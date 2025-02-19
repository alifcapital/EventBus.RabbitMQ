using System.Diagnostics;
using System.Text;
using System.Text.Json;
using EventBus.RabbitMQ.Connections;
using EventBus.RabbitMQ.Exceptions;
using EventBus.RabbitMQ.Instrumentation.Trace;
using EventBus.RabbitMQ.Subscribers.Managers;
using EventBus.RabbitMQ.Subscribers.Models;
using EventBus.RabbitMQ.Subscribers.Options;
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
    private readonly Dictionary<string, SubscribersInformation>
        _subscribers = new();

    public EventConsumerService(EventSubscriberOptions connectionOptions, IServiceProvider serviceProvider,
        bool useInbox)
    {
        _connectionOptions = connectionOptions;
        _serviceProvider = serviceProvider;
        _logger = _serviceProvider.GetRequiredService<ILogger<EventConsumerService>>();
        var rabbitMqConnectionCreator = serviceProvider.GetRequiredService<IRabbitMqConnectionCreator>();
        _connection = rabbitMqConnectionCreator.CreateConnection(connectionOptions, serviceProvider);
        _useInbox = useInbox;
    }

    public void AddSubscriber(SubscribersInformation eventInfo)
    {
        _subscribers.Add(eventInfo.Settings.EventTypeName!, eventInfo);
    }

    /// <summary>
    /// Starts receiving events by creating a consumer
    /// </summary>
    public void StartAndSubscribeReceiver()
    {
        _consumerChannel = CreateConsumerChannel();
        var consumer = new AsyncEventingBasicConsumer(_consumerChannel);
        consumer.Received += Consumer_Received;
        _consumerChannel.BasicConsume(queue: _connectionOptions.QueueName, autoAck: false, consumer: consumer);
    }

    /// <summary>
    /// To create channel for consumer
    /// </summary>
    /// <returns>Returns create channel</returns>
    private IModel CreateConsumerChannel()
    {
        _logger.LogTrace("Creating RabbitMQ consumer channel");

        var channel = _connection.CreateChannel();

        channel.ExchangeDeclare(exchange: _connectionOptions.VirtualHostSettings.ExchangeName,
            type: _connectionOptions.VirtualHostSettings.ExchangeType,
            durable: true, autoDelete: false);
        channel.QueueDeclare(_connectionOptions.QueueName, durable: true, exclusive: false, autoDelete: false,
            _connectionOptions.VirtualHostSettings.QueueArguments);
        foreach (var eventSettings in _subscribers.Values.Select(s => s.Settings))
            channel.QueueBind(_connectionOptions.QueueName, _connectionOptions.VirtualHostSettings.ExchangeName,
                eventSettings.RoutingKey);

        channel.CallbackException += (_, ea) =>
        {
            _logger.LogWarning(ea.Exception, "Recreating RabbitMQ consumer channel");

            _consumerChannel.Dispose();
            _consumerChannel = CreateConsumerChannel();
            StartAndSubscribeReceiver();
        };

        return channel;
    }

    private const string HandlerMethodName = nameof(IEventSubscriber<ISubscribeEvent>.HandleAsync);

    /// <summary>
    /// An event to receive all sent events
    /// </summary>
    private async Task Consumer_Received(object sender, BasicDeliverEventArgs eventArgs)
    {
        var eventType = eventArgs.BasicProperties.Type ?? eventArgs.RoutingKey;
        try
        {
            var eventPayload = Encoding.UTF8.GetString(eventArgs.Body.ToArray());

            Dictionary<string, string> headers;
            try
            {
                headers = GetEventHeaders();
            }
            catch (Exception e)
            {
                var eventPayloadData = $"{EventBusTraceInstrumentation.EventPayloadTag}: {eventPayload}";
                var eventHeadersData =
                    $"{EventBusTraceInstrumentation.EventHeadersTag}: {SerializeData(eventArgs.BasicProperties.Headers)}";
                _logger.LogError(e,
                    "----- ERROR while reading the headers of '{EventType}' event type with the '{RoutingKey}' routing key and '{EventId}' event id. {EventPayload}, {Headers}.",
                    eventType, eventArgs.RoutingKey, eventArgs.BasicProperties.MessageId, eventPayloadData,
                    eventHeadersData);

                return;
            }

            headers.TryGetValue(EventBusTraceInstrumentation.TraceParentIdKey, out var traceParentId);

            using var activity = EventBusTraceInstrumentation.StartActivity($"Received '{eventType}' event",
                ActivityKind.Consumer, traceParentId);

            if (EventBusTraceInstrumentation.ShouldAttachEventPayload)
                activity?.AddEvent(
                    new ActivityEvent($"{EventBusTraceInstrumentation.EventPayloadTag}: {eventPayload}"));

            var eventHeadersAsJson = SerializeData(headers);
            if (EventBusTraceInstrumentation.ShouldAttachEventHeaders)
                activity?.AddEvent(
                    new ActivityEvent($"{EventBusTraceInstrumentation.EventHeadersTag}: {eventHeadersAsJson}"));

            if (_subscribers.TryGetValue(eventType,
                    out var subscribersInformation))
            {
                _logger.LogTrace("Received RabbitMQ event, Type is {EventType} and EventId is {EventId}", subscribersInformation.EventTypeName,
                    eventArgs.BasicProperties.MessageId);
                var eventId = Guid.TryParse(eventArgs.BasicProperties.MessageId, out Guid messageId)
                    ? messageId
                    : Guid.NewGuid();
                
                using var scope = _serviceProvider.CreateScope();
                if (_useInbox)
                {
                    StoreEventToInbox(scope.ServiceProvider, subscribersInformation, eventId, eventPayload, eventHeadersAsJson);
                }
                else
                {
                    await ExecuteSubscribers(subscribersInformation, eventPayload, eventId, headers, scope.ServiceProvider);
                }

                MarkEventIsDelivered();
            }
            else
            {
                _logger.LogWarning(
                    "No subscription for '{EventType}' event with the '{RoutingKey}' routing key and '{EventId}' event id.",
                    eventType, eventArgs.RoutingKey, eventArgs.BasicProperties.MessageId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "----- ERROR on receiving '{EventType}' event type with the '{RoutingKey}' routing key and '{EventId}' event id.",
                eventType, eventArgs.RoutingKey, eventArgs.BasicProperties.MessageId);
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
            if (eventArgs.BasicProperties.Headers is not null)
            {
                foreach (var header in eventArgs.BasicProperties.Headers)
                {
                    var headerValue = header.Value is null ? null : Encoding.UTF8.GetString((byte[])header.Value);
                    eventHeaders.Add(header.Key, headerValue);
                }
            }

            return eventHeaders;
        }

        async Task ExecuteSubscribers(SubscribersInformation subscribersInformation, string eventPayload, Guid eventId, Dictionary<string, string> headers,
            IServiceProvider serviceProvider)
        {
            var jsonSerializerSetting = subscribersInformation.Settings.GetJsonSerializer();
            var virtualHost = _connectionOptions.VirtualHostSettings.VirtualHost;

            foreach (var subscriber in subscribersInformation.Subscribers)
            {
                var receivedEvent =
                    JsonSerializer.Deserialize(eventPayload, subscriber.EventType, jsonSerializerSetting) as
                        ISubscribeEvent;

                receivedEvent!.EventId = eventId;
                receivedEvent!.Headers = headers;

                EventSubscriberManager.OnExecutingSubscribedEvent(receivedEvent, virtualHost, serviceProvider);

                var eventHandlerSubscriber = serviceProvider.GetRequiredService(subscriber.EventSubscriberType);
                var handleMethod = subscriber.EventSubscriberType.GetMethod(HandlerMethodName);
                await ((Task)handleMethod!.Invoke(eventHandlerSubscriber, [receivedEvent]))!;
            }
        }

        void StoreEventToInbox(IServiceProvider serviceProvider, SubscribersInformation subscribersInformation, Guid eventId, string eventPayload, string eventHeadersAsJson)
        {
            var eventReceiverManager = serviceProvider.GetService<IEventReceiverManager>();
            if (eventReceiverManager is null)
                throw new EventBusException(
                    "The RabbitMQ is configured to use the Inbox for received events, but the Inbox functionality of the EventStorage is not enabled.");

            var namingPolicyType = subscribersInformation.Settings.PropertyNamingPolicy ?? NamingPolicyType.PascalCase;
            _ = eventReceiverManager.Received(eventId, subscribersInformation.EventTypeName, eventArgs.RoutingKey,
                EventProviderType.MessageBroker, payload: eventPayload, headers: eventHeadersAsJson,
                namingPolicyType: namingPolicyType);
        }

        #endregion
    }
}