using System.Diagnostics;
using System.Text;
using System.Text.Json;
using EventBus.RabbitMQ.Connections;
using EventBus.RabbitMQ.Exceptions;
using EventBus.RabbitMQ.Extensions;
using EventBus.RabbitMQ.Instrumentation;
using EventBus.RabbitMQ.Instrumentation.Trace;
using EventBus.RabbitMQ.Subscribers.Managers;
using EventBus.RabbitMQ.Subscribers.Models;
using EventBus.RabbitMQ.Subscribers.Options;
using EventStorage.Inbox.EventArgs;
using EventStorage.Inbox.Managers;
using EventStorage.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace EventBus.RabbitMQ.Subscribers.Consumers;

internal class EventConsumerService : IEventConsumerService
{
    private readonly bool _useInbox;
    private readonly EventSubscriberOptions _connectionOptions;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EventConsumerService> _logger;
    private readonly IRabbitMqConnection _connection;
    private IModel _consumerChannel;

    /// <summary>
    /// Dictionary collection to store all events and event handlers information
    /// </summary>
    private readonly Dictionary<string, SubscribersInformation> _subscribers = [];

    /// <summary>
    /// The event to be executed after executing all subscribers of the event.
    /// </summary>
    public static event EventHandler<EventHandlerArgs> EventSubscribersHandled;

    public EventConsumerService(EventSubscriberOptions connectionOptions, IServiceProvider serviceProvider,
        bool useInbox)
    {
        _connectionOptions = connectionOptions;
        _serviceProvider = serviceProvider;
        _logger = _serviceProvider.GetRequiredService<ILogger<EventConsumerService>>();
        var rabbitMqConnectionCreator = serviceProvider.GetRequiredService<IRabbitMqConnectionManager>();
        _connection = rabbitMqConnectionCreator.GetOrCreateConnection(connectionOptions.VirtualHostSettings);
        _useInbox = useInbox;
    }

    public void AddSubscriber(SubscribersInformation eventInfo)
    {
        _subscribers.Add(eventInfo.Settings.EventTypeName!, eventInfo);
    }

    #region Create channel and subscribe receiver

    private readonly Lock _lockReOpenChannel = new();

    /// <summary>
    /// Starts receiving events by creating a consumer
    /// </summary>
    public void CreateChannelAndSubscribeReceiver()
    {
        _consumerChannel = CreateConsumerChannel();
        var consumer = new AsyncEventingBasicConsumer(_consumerChannel);
        consumer.Received += Consumer_ReceivingEvent;
        _consumerChannel.BasicConsume(queue: _connectionOptions.QueueName, autoAck: false, consumer: consumer);
    }

    /// <summary>
    /// To create channel for consumer. If the channel is disconnected, it will try to create a new one.
    /// </summary>
    /// <returns>Returns create channel</returns>
    private IModel CreateConsumerChannel()
    {
        _logger.LogTrace("Creating RabbitMQ consumer channel");

        var channel = _connection.CreateChannel();

        var virtualHostSettings = _connectionOptions.VirtualHostSettings;
        channel.ExchangeDeclare(
            exchange: virtualHostSettings.ExchangeName,
            type: virtualHostSettings.ExchangeType,
            durable: true,
            autoDelete: false,
            arguments: virtualHostSettings.ExchangeArguments);

        channel.QueueDeclare(
            queue: _connectionOptions.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: virtualHostSettings.QueueArguments);
        foreach (var eventSettings in _subscribers.Values.Select(s => s.Settings))
            channel.QueueBind(_connectionOptions.QueueName, virtualHostSettings.ExchangeName,
                eventSettings.RoutingKey);

        channel.CallbackException += OnCallbackException;

        return channel;
    }

    /// <summary>
    /// The event handler for recreating the consumer channel when an exception is thrown.
    /// </summary>
    private void OnCallbackException(object sender, CallbackExceptionEventArgs e)
    {
        lock (_lockReOpenChannel)
        {
            _logger.LogWarning(e.Exception, "Recreating RabbitMQ consumer channel after exception");

            _consumerChannel.CallbackException -= OnCallbackException;
            _consumerChannel.Dispose();

            CreateChannelAndSubscribeReceiver();
        }
    }

    #endregion

    #region Receiving and handling events

    /// <summary>
    /// An event to receive all sent events
    /// </summary>
    private async Task Consumer_ReceivingEvent(object sender, BasicDeliverEventArgs eventArgs)
    {
        var eventType = eventArgs.BasicProperties.Type ?? eventArgs.RoutingKey;
        try
        {
            var scopedTags = new Dictionary<string, object>
            {
                { EventBusInvestigationTagNames.ReceivedEventIdTag, eventArgs.BasicProperties.MessageId },
                { EventBusInvestigationTagNames.ReceivedEventTypeTag, eventArgs.BasicProperties.Type },
                { EventBusInvestigationTagNames.ReceivedEventRoutingKeyTag, eventArgs.RoutingKey }
            };
            using var _ = _logger.BeginScope(scopedTags);
            _logger.LogDebug("Receiving RabbitMQ event type is '{EventType}'", eventType);

            var eventPayload = Encoding.UTF8.GetString(eventArgs.Body.ToArray());

            Dictionary<string, string> headers;
            try
            {
                headers = GetEventHeaders();
            }
            catch (Exception e)
            {
                var eventPayloadData = $"{EventBusInvestigationTagNames.EventPayloadTag}: {eventPayload}";
                var eventHeadersData =
                    $"{EventBusInvestigationTagNames.EventHeadersTag}: {SerializeData(eventArgs.BasicProperties.Headers)}";
                _logger.LogError(e,
                    "Error while reading the headers of event '{EventType}'. The payload: '{EventPayload}' and headers is '{EventHeaders}'.",
                    eventType, eventPayloadData, eventHeadersData);

                return;
            }

            headers.TryGetValue(EventBusTraceInstrumentation.TraceParentIdKey, out var traceParentId);

            using var activity = EventBusTraceInstrumentation.StartActivity($"MQ: Received event '{eventType}'",
                ActivityKind.Consumer, traceParentId);
            activity?.AddTags(scopedTags);

            if (EventBusTraceInstrumentation.ShouldAttachEventPayload)
                activity?.AddEvent(
                    new ActivityEvent($"{EventBusInvestigationTagNames.EventPayloadTag}: {eventPayload}"));

            var eventHeadersAsJson = SerializeData(headers);
            if (EventBusTraceInstrumentation.ShouldAttachEventHeaders)
                activity?.AddEvent(
                    new ActivityEvent($"{EventBusInvestigationTagNames.EventHeadersTag}: {eventHeadersAsJson}"));

            if (_subscribers.TryGetValue(eventType,
                    out var subscribersInformation))
            {
                var eventId = Guid.TryParse(eventArgs.BasicProperties.MessageId, out var messageId)
                    ? messageId
                    : Guid.NewGuid();
                _logger.LogDebug("Received RabbitMQ event '{EventType}' (ID: {EventId})",
                    subscribersInformation.EventTypeName, eventId);
                
                headers.TryGetValue(EventBusInvestigationTagNames.EventNamingPolicyTypeTag, out var eventNamingPolicy);
                var configuredNamingPolicy = subscribersInformation.Settings.PropertyNamingPolicy!.ToString();
                if (!string.IsNullOrEmpty(eventNamingPolicy) && configuredNamingPolicy != eventNamingPolicy)
                {
                    var message = $"The naming policy type of received event '{eventType}' ({eventNamingPolicy}) is different from the configured naming policy type ({configuredNamingPolicy}). Deserialization issues may occur.";
                    throw new EventBusException(message);
                }

                using var scope = _serviceProvider.CreateScope();
                if (_useInbox)
                {
                    StoreEventToInbox(scope.ServiceProvider, subscribersInformation, eventId, eventPayload,
                        eventHeadersAsJson);
                }
                else
                {
                    await ExecuteSubscribers(subscribersInformation, eventPayload, eventId, headers,
                        scope.ServiceProvider);
                }

                MarkEventIsDelivered();
            }
            else
            {
                _logger.LogWarning("No subscription for '{EventType}' RabbitMQ event.", eventType);
            }
        }
        catch (Exception ex)
        {
            var innerMessage = ex is EventBusException ? ex.Message : null;
            _logger.LogError(ex,
                "Error while receiving '{EventType}' event with the '{RoutingKey}' routing key and '{EventId}' event id. {InnerMessage}",
                eventType, eventArgs.RoutingKey, eventArgs.BasicProperties.MessageId, innerMessage);
        }

        #region Helper methods

        void MarkEventIsDelivered()
        {
            _consumerChannel.BasicAck(eventArgs.DeliveryTag, multiple: false);
        }

        static string SerializeData<TValue>(TValue data)
        {
            return JsonSerializer.Serialize(data, data.GetType());
        }

        Dictionary<string, string> GetEventHeaders()
        {
            var eventHeaders = new Dictionary<string, string>();
            if (eventArgs.BasicProperties.Headers is null) return eventHeaders;

            foreach (var header in eventArgs.BasicProperties.Headers)
            {
                if (header.Value is null)
                    continue;

                string headerValue;
                if (header.Value is byte[] headerValueBytes)
                    headerValue = Encoding.UTF8.GetString(headerValueBytes);
                else
                    headerValue = header.Value.ToString();

                eventHeaders.Add(header.Key, headerValue);
            }

            return eventHeaders;
        }

        async Task ExecuteSubscribers(SubscribersInformation subscribersInformation, string eventPayload, Guid eventId,
            Dictionary<string, string> headers,
            IServiceProvider serviceProvider)
        {
            var jsonSerializerSetting = subscribersInformation.Settings.GetJsonSerializer();
            var virtualHost = _connectionOptions.VirtualHostSettings.VirtualHost;

            _logger.LogDebug("Executing {SubscriberCount} subscribers of received event '{EventTypeName}'",
                subscribersInformation.Subscribers.Count, subscribersInformation.EventTypeName);

            foreach (var subscriber in subscribersInformation.Subscribers)
            {
                var receivedEvent =
                    JsonSerializer.Deserialize(eventPayload, subscriber.EventType, jsonSerializerSetting) as
                        ISubscribeEvent;

                receivedEvent!.EventId = eventId;
                receivedEvent!.Headers = headers;

                EventSubscriberCollector.OnExecutingSubscribedEvent(receivedEvent, virtualHost, serviceProvider);

                var eventHandlerSubscriber = serviceProvider.GetRequiredService(subscriber.EventSubscriberType);
                await ((Task)subscriber.HandleMethod!.Invoke(eventHandlerSubscriber, [receivedEvent]))!;
            }

            OnAllEventSubscribersAreHandled(subscribersInformation.EventTypeName, serviceProvider);
        }

        void StoreEventToInbox(IServiceProvider serviceProvider, SubscribersInformation subscribersInformation,
            Guid eventId, string eventPayload, string eventHeadersAsJson)
        {
            var inboxEventManager = serviceProvider.GetService<IInboxEventManager>();
            if (inboxEventManager is null)
                throw new EventBusException(
                    "The RabbitMQ is configured to use the Inbox for received events, but the Inbox functionality of the EventStorage is not enabled.");

            var namingPolicyType = subscribersInformation.Settings.PropertyNamingPolicy ?? NamingPolicyType.PascalCase;
            _ = inboxEventManager.Store(eventId, subscribersInformation.EventTypeName,
                EventProviderType.MessageBroker,
                payload: eventPayload,
                headers: eventHeadersAsJson,
                eventPath: eventArgs.RoutingKey,
                namingPolicyType: namingPolicyType);
        }

        #endregion
    }

    #endregion

    #region Helper methods

    /// <summary>
    /// Invokes the <see cref="EventSubscribersHandled"/> event to be able to execute the event after the subscriber.
    /// </summary>
    /// <param name="eventName">The name of executed event.</param>
    /// <param name="serviceProvider">The IServiceProvider used to resolve dependencies from the scope.</param>
    private void OnAllEventSubscribersAreHandled(string eventName, IServiceProvider serviceProvider)
    {
        if (EventSubscribersHandled is null)
            return;

        const string eventProviderType = nameof(EventProviderType.MessageBroker);
        var eventArgs = new EventHandlerArgs
        {
            EventName = eventName,
            EventProviderType = eventProviderType,
            ServiceProvider = serviceProvider
        };
        EventSubscribersHandled.Invoke(this, eventArgs);
    }

    #endregion
}