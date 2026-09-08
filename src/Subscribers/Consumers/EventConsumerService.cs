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
    private IChannel _consumerChannel;
    private CancellationToken _serviceCancellationToken;

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
        try
        {
            _connectionOptions = connectionOptions;
            _serviceProvider = serviceProvider;
            _logger = _serviceProvider.GetRequiredService<ILogger<EventConsumerService>>();
            var rabbitMqConnectionCreator = serviceProvider.GetRequiredService<IRabbitMqConnectionManager>();
            _connection = rabbitMqConnectionCreator.GetOrCreateConnection(connectionOptions.VirtualHostSettings);
            _useInbox = useInbox;
        }
        catch (Exception e)
        {
            throw new EventBusException(e,
                $"Error while creating RabbitMQ event consumer service for '{connectionOptions.QueueName}' queue of '{connectionOptions.VirtualHostSettings.VirtualHost}' virtual host.");
        }
    }

    public void AddSubscriber(SubscribersInformation eventInfo)
    {
        _subscribers.Add(eventInfo.Settings.EventTypeName!, eventInfo);
    }

    #region Create channel and subscribe receiver

    private readonly SemaphoreSlim _reopenChannelGate = new(1, 1);

    /// <summary>
    /// The delay between retry attempts when the channel fails to be created and the receiver fails to subscribe.
    /// </summary>
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Starts receiving events by creating a consumer. If it fails, the failure is logged and a background retry
    /// loop is started, retrying every <see cref="RetryDelay"/> until it succeeds or the cancellation is requested.
    /// </summary>
    public async Task CreateChannelAndSubscribeReceiverAsync(CancellationToken cancellationToken)
    {
        _serviceCancellationToken = cancellationToken;
        try
        {
            await CreateChannelAndStartConsumingAsync(cancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogError(e,
                "Error while creating a channel and subscribing to a consumer for '{QueueName}' queue of '{VirtualHost}' virtual host. Will keep retrying every {RetryDelay} until it succeeds.",
                _connectionOptions.QueueName, _connectionOptions.VirtualHostSettings.VirtualHost, RetryDelay);

            _ = RetryCreateChannelAndSubscribeReceiverAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Keeps retrying to create a channel and subscribe the receiver every <see cref="RetryDelay"/> until it
    /// succeeds or the cancellation is requested.
    /// </summary>
    private async Task RetryCreateChannelAndSubscribeReceiverAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(RetryDelay, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                await CreateChannelAndStartConsumingAsync(cancellationToken);
                _logger.LogInformation(
                    "Successfully created a channel and subscribed a consumer for '{QueueName}' queue of '{VirtualHost}' virtual host after retrying.",
                    _connectionOptions.QueueName, _connectionOptions.VirtualHostSettings.VirtualHost);
                return;
            }
            catch (Exception e)
            {
                _logger.LogError(e,
                    "Retry failed while creating a channel and subscribing to a consumer for '{QueueName}' queue of '{VirtualHost}' virtual host. Will retry again in {RetryDelay}.",
                    _connectionOptions.QueueName, _connectionOptions.VirtualHostSettings.VirtualHost, RetryDelay);
            }
        }
    }

    /// <summary>
    /// Creates the consumer channel and starts consuming from the queue. Throws if either step fails.
    /// </summary>
    private async Task CreateChannelAndStartConsumingAsync(CancellationToken cancellationToken)
    {
        try
        {
            _consumerChannel = await CreateConsumerChannelAsync(cancellationToken);
            var consumer = new AsyncEventingBasicConsumer(_consumerChannel);
            consumer.ReceivedAsync += Consumer_ReceivingEvent;
            _ = await _consumerChannel.BasicConsumeAsync(
                queue: _connectionOptions.QueueName,
                autoAck: false,
                consumer: consumer,
                cancellationToken: cancellationToken);
        }
        catch (IOException e)
        {
            throw new EventBusException(e,
                $"Error while creating RabbitMQ consumer channel for '{_connectionOptions.QueueName}' queue of '{_connectionOptions.VirtualHostSettings.VirtualHost}' virtual host.");
        }
    }

    /// <summary>
    /// To create a channel for consumer. If the channel is disconnected, it will try to create a new one.
    /// </summary>
    /// <returns>Returns create a channel</returns>
    private async Task<IChannel> CreateConsumerChannelAsync(CancellationToken cancellationToken)
    {
        _logger.LogTrace("Creating RabbitMQ consumer channel");

        var channel = await _connection.CreateConsumerChannelAsync(cancellationToken);

        var virtualHostSettings = _connectionOptions.VirtualHostSettings;
        await channel.ExchangeDeclareAsync(
            exchange: virtualHostSettings.ExchangeName,
            type: virtualHostSettings.ExchangeType,
            durable: true,
            autoDelete: false,
            arguments: virtualHostSettings.ExchangeArguments,
            noWait: false,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queue: _connectionOptions.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: virtualHostSettings.QueueArguments,
            noWait: false,
            cancellationToken: cancellationToken);
        foreach (var eventSettings in _subscribers.Values.Select(s => s.Settings))
            await channel.QueueBindAsync(
                queue: _connectionOptions.QueueName,
                exchange: virtualHostSettings.ExchangeName,
                routingKey: eventSettings.RoutingKey,
                arguments: null,
                noWait: false,
                cancellationToken: cancellationToken);

        channel.CallbackExceptionAsync += OnCallbackExceptionAsync;
        channel.ChannelShutdownAsync += OnChannelShutdownAsync;

        return channel;
    }

    #endregion

    #region Channel events

    /// <summary>
    /// The event handler for recreating the consumer channel when an exception is thrown.
    /// </summary>
    private async Task OnCallbackExceptionAsync(object sender, CallbackExceptionEventArgs e)
    {
        try
        {
            await _reopenChannelGate.WaitAsync(_serviceCancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        try
        {
            _logger.LogWarning(e.Exception,
                "Recreating RabbitMQ consumer channel for '{QueueName}' queue of '{VirtualHost}' virtual host after exception.",
                _connectionOptions.QueueName, _connectionOptions.VirtualHostSettings.VirtualHost);

            try
            {
                UnsubscribeChannelEvents(_consumerChannel);
                _consumerChannel.Dispose();
            }
            catch (Exception ex)
            {
                var message =
                    $"Error while recreating RabbitMQ consumer channel for '{_connectionOptions.QueueName}' queue of '{_connectionOptions.VirtualHostSettings.VirtualHost}' virtual host after exception";
                _logger.LogError(ex, message);
                throw new EventBusException(ex, message);
            }
        }
        finally
        {
            _reopenChannelGate.Release();
        }

        await CreateChannelAndSubscribeReceiverAsync(_serviceCancellationToken);
    }

    /// <summary>
    /// The event handler for recreating the consumer channel when it is shut down unexpectedly, e.g. because the
    /// underlying RabbitMQ connection dropped and was reconnected by <see cref="IRabbitMqConnection"/>.
    /// </summary>
    private async Task OnChannelShutdownAsync(object sender, ShutdownEventArgs reason)
    {
        try
        {
            await _reopenChannelGate.WaitAsync(_serviceCancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        try
        {
            _logger.LogWarning(
                "Recreating RabbitMQ consumer channel for '{QueueName}' queue of '{VirtualHost}' virtual host after it was shut down. Reason: {Reason}",
                _connectionOptions.QueueName, _connectionOptions.VirtualHostSettings.VirtualHost, reason);

            UnsubscribeChannelEvents(_consumerChannel);
            _consumerChannel.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error while disposing the shut down RabbitMQ consumer channel for '{QueueName}' queue of '{VirtualHost}' virtual host.",
                _connectionOptions.QueueName, _connectionOptions.VirtualHostSettings.VirtualHost);
        }
        finally
        {
            _reopenChannelGate.Release();
        }

        await CreateChannelAndSubscribeReceiverAsync(_serviceCancellationToken);
    }

    /// <summary>
    /// Unsubscribes this instance's handlers from the channel's events, so disposing the channel ourselves
    /// doesn't cause a re-entrant call into <see cref="OnChannelShutdownAsync"/>.
    /// </summary>
    private void UnsubscribeChannelEvents(IChannel channel)
    {
        channel.CallbackExceptionAsync -= OnCallbackExceptionAsync;
        channel.ChannelShutdownAsync -= OnChannelShutdownAsync;
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

                if (headers.TryGetValue(EventBusInvestigationTagNames.EventNamingPolicyTypeTag,
                        out var eventNamingPolicy))
                {
                    var configuredNamingPolicy = subscribersInformation.Settings.PropertyNamingPolicy.ToString();
                    if (configuredNamingPolicy != eventNamingPolicy)
                    {
                        var message =
                            $"The naming policy type of received event '{eventType}' ({eventNamingPolicy}) is different from the configured naming policy type ({configuredNamingPolicy}). Deserialization issues may occur.";
                        throw new EventBusException(message);
                    }
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

                await MarkEventIsDeliveredAsync();
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

        ValueTask MarkEventIsDeliveredAsync()
        {
            return _consumerChannel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false,
                cancellationToken: _serviceCancellationToken);
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
            var inboxEventManager = serviceProvider.GetRequiredService<IInboxEventManager>();
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
