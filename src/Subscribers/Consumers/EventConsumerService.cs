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
using RabbitMQ.Client.Exceptions;

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
    /// The tag/id of newly created consumer channel to use for closing the opened channel if needed.
    /// </summary>
    private string _consumerTag;

    /// <summary>
    /// Whether the consumer is stopping to receive events, e.g. because the application is shutting down.
    /// </summary>
    private volatile bool _isStopping;

    /// <summary>
    /// The number of received events that are being handled at the moment.
    /// </summary>
    private int _handlingEventsCount;

    /// <summary>
    /// The interval for checking whether the events being handled are completed while stopping.
    /// </summary>
    private static readonly TimeSpan HandlingEventsCheckInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Whether the errors are caused by stopping the application (e.g. the disposed service provider).
    /// </summary>
    private bool IsStopping => _isStopping || _serviceCancellationToken.IsCancellationRequested;

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
    /// Id of the connection the current channel was created on, to detect a channel of a lost connection.
    /// </summary>
    private Guid _consumerChannelConnectionId;

    /// <summary>
    /// 1 while <see cref="RetryCreateChannelAndSubscribeReceiverAsync"/> is running, otherwise 0, so only one retry
    /// loop is started. An <see cref="int"/> instead of a <see cref="bool"/>, to be used with <see cref="Interlocked"/>.
    /// </summary>
    private int _isRetrying;

    /// <summary>
    /// 1 while subscribed to <see cref="IRabbitMqConnection.Reconnected"/>, otherwise 0, so it is subscribed only once.
    /// An <see cref="int"/> instead of a <see cref="bool"/>, to be used with <see cref="Interlocked"/>.
    /// </summary>
    private int _isSubscribedToReconnected;

    /// <summary>
    /// Starts receiving events by creating a consumer. If it fails, the failure is logged and a background retry
    /// loop is started, retrying every <see cref="RetryDelay"/> until it succeeds or the cancellation is requested.
    /// </summary>
    public async Task CreateChannelAndSubscribeReceiverAsync(CancellationToken cancellationToken)
    {
        _serviceCancellationToken = cancellationToken;
        if (!_isStopping)
        {
            var shouldSubscribeToReconnected = Interlocked.Exchange(ref _isSubscribedToReconnected, 1) == 0;
            if (shouldSubscribeToReconnected)
                _connection.Reconnected += OnConnectionReconnected;
        }

        await RecreateConsumerOrStartRetryingAsync(channelToRecreate: null, cancellationToken);
    }

    /// <summary>
    /// Runs <see cref="RecreateConsumerIfNotUsableAsync"/>; on failure, logs it and starts the retry loop.
    /// </summary>
    /// <param name="channelToRecreate">A channel known to be broken, recreated even if it reports open.</param>
    /// <param name="cancellationToken">The application's stopping token.</param>
    private async Task RecreateConsumerOrStartRetryingAsync(IChannel channelToRecreate,
        CancellationToken cancellationToken)
    {
        try
        {
            await RecreateConsumerIfNotUsableAsync(channelToRecreate, cancellationToken);
        }
        catch (Exception) when (IsStopping)
        {
            // The application is stopping, so the error is caused by the shutdown and there is no need to retry.
        }
        catch (Exception e)
        {
            _logger.LogError(e,
                "Error while creating a channel and subscribing to a consumer for '{QueueName}' queue of '{VirtualHost}' virtual host. Will keep retrying every {RetryDelay} until it succeeds.",
                _connectionOptions.QueueName, _connectionOptions.VirtualHostSettings.VirtualHost, RetryDelay);

            var shouldStartRetryLoop = Interlocked.Exchange(ref _isRetrying, 1) == 0;
            if (shouldStartRetryLoop)
                _ = RetryCreateChannelAndSubscribeReceiverAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Keeps retrying to create a channel and subscribe the receiver every <see cref="RetryDelay"/> until it
    /// succeeds or the cancellation is requested.
    /// </summary>
    private async Task RetryCreateChannelAndSubscribeReceiverAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && !_isStopping)
            {
                try
                {
                    await Task.Delay(RetryDelay, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (_isStopping) break;

                try
                {
                    var isCreated = await RecreateConsumerIfNotUsableAsync(channelToRecreate: null, cancellationToken);
                    if (isCreated)
                    {
                        _logger.LogInformation(
                            "Successfully created a channel and subscribed a consumer for '{QueueName}' queue of '{VirtualHost}' virtual host after retrying.",
                            _connectionOptions.QueueName, _connectionOptions.VirtualHostSettings.VirtualHost);
                    }
                    else
                    {
                        _logger.LogDebug(
                            "Stopped retrying: the consumer of '{QueueName}' queue of '{VirtualHost}' virtual host is already running.",
                            _connectionOptions.QueueName, _connectionOptions.VirtualHostSettings.VirtualHost);
                    }

                    return;
                }
                catch (Exception) when (IsStopping)
                {
                    // The application is stopping, so the error is caused by the shutdown and there is no need to retry.
                    break;
                }
                catch (Exception e)
                {
                    _logger.LogError(e,
                        "Retry failed while creating a channel and subscribing to a consumer for '{QueueName}' queue of '{VirtualHost}' virtual host. Will retry again in {RetryDelay}.",
                        _connectionOptions.QueueName, _connectionOptions.VirtualHostSettings.VirtualHost, RetryDelay);
                }
            }
        }
        finally
        {
            Interlocked.Exchange(ref _isRetrying, 0);
        }
    }

    /// <summary>
    /// (Re)creates the consumer, unless its channel is open on the current connection. The old channel is disposed
    /// first, so the service never has two consumers on the queue. Throws if creating fails.
    /// </summary>
    /// <param name="channelToRecreate">A channel known to be broken, recreated even if it reports open.</param>
    /// <param name="cancellationToken">The application's stopping token.</param>
    /// <returns>True if a consumer was created; false if the current one is still usable or the service is stopping.</returns>
    private async Task<bool> RecreateConsumerIfNotUsableAsync(IChannel channelToRecreate,
        CancellationToken cancellationToken)
    {
        await _reopenChannelGate.WaitAsync(cancellationToken);
        try
        {
            if (_isStopping) return false;

            var currentChannel = _consumerChannel;
            if (currentChannel is not null)
            {
                var isCurrentChannelBroken = ReferenceEquals(currentChannel, channelToRecreate);
                if (!isCurrentChannelBroken && currentChannel.IsOpen &&
                    _consumerChannelConnectionId == _connection.ConnectionId)
                    return false;

                _logger.LogWarning(
                    "Recreating RabbitMQ consumer channel for '{QueueName}' queue of '{VirtualHost}' virtual host.",
                    _connectionOptions.QueueName, _connectionOptions.VirtualHostSettings.VirtualHost);

                DisposeChannel(currentChannel);
                _consumerChannel = null;
                _consumerTag = null;
            }

            await CreateChannelAndStartConsumingAsync(cancellationToken);
            return true;
        }
        finally
        {
            _reopenChannelGate.Release();
        }
    }

    /// <summary>
    /// Creates the consumer channel and starts consuming from the queue. Throws if either step fails.
    /// </summary>
    private async Task CreateChannelAndStartConsumingAsync(CancellationToken cancellationToken)
    {
        IChannel channel = null;
        try
        {
            channel = await CreateConsumerChannelAsync(cancellationToken);
            // Read right after creating the channel, so a reconnect during BasicConsumeAsync is not missed.
            // Otherwise, a channel of the lost connection could get the new connection's ID if that is recreated.
            var channelConnectionId = _connection.ConnectionId;
            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += Consumer_ReceivingEvent;
            var consumerTag = await channel.BasicConsumeAsync(
                queue: _connectionOptions.QueueName,
                autoAck: false,
                consumer: consumer,
                cancellationToken: cancellationToken);

            _consumerChannel = channel;
            _consumerChannelConnectionId = channelConnectionId;
            _consumerTag = consumerTag;
        }
        catch (Exception e)
        {
            if (channel is not null)
                DisposeChannel(channel);

            if (e is IOException)
                throw new EventBusException(e,
                    $"Error while creating RabbitMQ consumer channel for '{_connectionOptions.QueueName}' queue of '{_connectionOptions.VirtualHostSettings.VirtualHost}' virtual host.");

            throw;
        }
    }

    /// <summary>
    /// Creates a consumer channel and declares its exchange, queue and bindings. Disposes the channel if any of them fails.
    /// </summary>
    /// <returns>The created channel.</returns>
    private async Task<IChannel> CreateConsumerChannelAsync(CancellationToken cancellationToken)
    {
        _logger.LogTrace("Creating RabbitMQ consumer channel");

        var channel = await _connection.CreateConsumerChannelAsync(cancellationToken);
        try
        {
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

            var eventsSettings = _subscribers.Values.Select(s => s.Settings);
            foreach (var eventSettings in eventsSettings)
                await channel.QueueBindAsync(
                    queue: _connectionOptions.QueueName,
                    exchange: virtualHostSettings.ExchangeName,
                    routingKey: eventSettings.RoutingKey,
                    arguments: null,
                    noWait: false,
                    cancellationToken: cancellationToken);
        }
        catch
        {
            DisposeChannel(channel);
            throw;
        }

        channel.CallbackExceptionAsync += OnCallbackExceptionAsync;
        channel.ChannelShutdownAsync += OnChannelShutdownAsync;

        return channel;
    }

    /// <summary>
    /// Unsubscribes from the channel's events and disposes it, ignoring errors (it is usually already broken).
    /// </summary>
    private void DisposeChannel(IChannel channel)
    {
        UnsubscribeChannelEvents(channel);
        try
        {
            channel.Dispose();
        }
        catch (Exception e)
        {
            _logger.LogDebug(e,
                "Error while disposing the old RabbitMQ consumer channel for '{QueueName}' queue of '{VirtualHost}' virtual host.",
                _connectionOptions.QueueName, _connectionOptions.VirtualHostSettings.VirtualHost);
        }
    }

    #endregion

    #region Channel and connection events

    /// <summary>
    /// Recreates the consumer channel after an exception in it.
    /// </summary>
    private Task OnCallbackExceptionAsync(object sender, CallbackExceptionEventArgs e)
    {
        if (IsStopping) return Task.CompletedTask;

        _logger.LogWarning(e.Exception,
            "An exception is thrown in the RabbitMQ consumer channel for '{QueueName}' queue of '{VirtualHost}' virtual host. The channel will be recreated.",
            _connectionOptions.QueueName, _connectionOptions.VirtualHostSettings.VirtualHost);

        StartRecreatingConsumer(sender as IChannel ?? _consumerChannel);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Recreates the consumer channel after it is shut down, e.g. because its connection is lost.
    /// </summary>
    private Task OnChannelShutdownAsync(object sender, ShutdownEventArgs reason)
    {
        if (IsStopping) return Task.CompletedTask;

        _logger.LogWarning(
            "The RabbitMQ consumer channel for '{QueueName}' queue of '{VirtualHost}' virtual host is shut down. The channel will be recreated. Reason: {Reason}",
            _connectionOptions.QueueName, _connectionOptions.VirtualHostSettings.VirtualHost, reason);

        StartRecreatingConsumer(sender as IChannel ?? _consumerChannel);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Recreates the consumer after a reconnect, in case its channel missed the shutdown notification.
    /// </summary>
    private void OnConnectionReconnected(object sender, EventArgs e)
    {
        if (IsStopping) return;

        StartRecreatingConsumer(channelToRecreate: null);
    }

    /// <summary>
    /// Recreates the consumer in the background, so the RabbitMQ client's event handlers are not blocked
    /// (the client notifies the channels of a lost connection one by one).
    /// </summary>
    private void StartRecreatingConsumer(IChannel channelToRecreate)
    {
        var cancellationToken = _serviceCancellationToken;
        _ = Task.Run(() => RecreateConsumerOrStartRetryingAsync(channelToRecreate, cancellationToken),
            CancellationToken.None);
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

    #region Stop receiving events

    public async Task StopReceivingEventsAsync(CancellationToken cancellationToken)
    {
        _isStopping = true;
        var shouldUnsubscribeFromReconnected = Interlocked.Exchange(ref _isSubscribedToReconnected, 0) == 1;
        if (shouldUnsubscribeFromReconnected)
            _connection.Reconnected -= OnConnectionReconnected;

        // Wait for a channel being recreated right now, so it does not keep consuming after the stop.
        var isGateAcquired = false;
        try
        {
            await _reopenChannelGate.WaitAsync(cancellationToken);
            isGateAcquired = true;
        }
        catch (OperationCanceledException)
        {
        }

        IChannel channel;
        Guid channelConnectionId;
        try
        {
            channel = _consumerChannel;
            if (channel is null) return;

            channelConnectionId = _consumerChannelConnectionId;
            UnsubscribeChannelEvents(channel);
        }
        finally
        {
            if (isGateAcquired)
                _reopenChannelGate.Release();
        }

        // A channel of a lost connection may still report open, but it cannot be used.
        var isChannelUsable = channel.IsOpen && channelConnectionId == _connection.ConnectionId;
        try
        {
            if (_consumerTag is not null && isChannelUsable)
                await channel.BasicCancelAsync(_consumerTag, noWait: false, cancellationToken);
        }
        catch (AlreadyClosedException e)
        {
            _logger.LogDebug(e,
                "The RabbitMQ consumer of '{QueueName}' queue of '{VirtualHost}' virtual host is already closed.",
                _connectionOptions.QueueName, _connectionOptions.VirtualHostSettings.VirtualHost);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _logger.LogWarning(e,
                "Error while cancelling the RabbitMQ consumer of '{QueueName}' queue of '{VirtualHost}' virtual host.",
                _connectionOptions.QueueName, _connectionOptions.VirtualHostSettings.VirtualHost);
        }

        try
        {
            // Waits until all processing events are handled.
            while (Volatile.Read(ref _handlingEventsCount) > 0)
                await Task.Delay(HandlingEventsCheckInterval, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "Stopped waiting for {HandlingEventsCount} event(s) being handled by the consumer of '{QueueName}' queue of '{VirtualHost}' virtual host.",
                Volatile.Read(ref _handlingEventsCount), _connectionOptions.QueueName,
                _connectionOptions.VirtualHostSettings.VirtualHost);
        }

        try
        {
            // Closing the channel makes RabbitMQ redeliver the received but unacknowledged events.
            if (isChannelUsable && channel.IsOpen)
                await channel.CloseAsync(CancellationToken.None);
        }
        catch (Exception e)
        {
            _logger.LogDebug(e,
                "Error while closing the RabbitMQ consumer channel of '{QueueName}' queue of '{VirtualHost}' virtual host.",
                _connectionOptions.QueueName, _connectionOptions.VirtualHostSettings.VirtualHost);
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
        // Increment the handling events counter.
        Interlocked.Increment(ref _handlingEventsCount);
        try
        {
            if (_isStopping)
            {
                // The event is not acknowledged, so RabbitMQ redelivers it after the channel is closed.
                _logger.LogDebug(
                    "The consumer is stopping. Skipped receiving '{EventType}' event with the '{RoutingKey}' routing key and '{EventId}' event id.",
                    eventType, eventArgs.RoutingKey, eventArgs.BasicProperties.MessageId);
                return;
            }

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
                    await StoreEventToInboxAsync(scope.ServiceProvider, subscribersInformation, eventId, eventPayload,
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
        catch (Exception ex) when (IsStopping)
        {
            // The application is stopping, so the error is caused by the shutdown (e.g. the disposed service provider).
            // The event is not acknowledged, so RabbitMQ redelivers it after the restart.
            _logger.LogDebug(ex,
                "The application is stopping. Skipped receiving '{EventType}' event with the '{RoutingKey}' routing key and '{EventId}' event id.",
                eventType, eventArgs.RoutingKey, eventArgs.BasicProperties.MessageId);
        }
        catch (Exception ex)
        {
            var innerMessage = ex is EventBusException ? ex.Message : null;
            _logger.LogError(ex,
                "Error while receiving '{EventType}' event with the '{RoutingKey}' routing key and '{EventId}' event id. {InnerMessage}",
                eventType, eventArgs.RoutingKey, eventArgs.BasicProperties.MessageId, innerMessage);
        }
        finally
        {
            // Decrement the handling events counter.
            Interlocked.Decrement(ref _handlingEventsCount);
        }

        #region Helper methods

        ValueTask MarkEventIsDeliveredAsync()
        {
            // The event is already handled, so it must be acknowledged even if the application is stopping.
            return _consumerChannel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false,
                cancellationToken: CancellationToken.None);
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

        async Task StoreEventToInboxAsync(IServiceProvider serviceProvider,
            SubscribersInformation subscribersInformation, Guid eventId, string eventPayload,
            string eventHeadersAsJson)
        {
            var inboxEventManager = serviceProvider.GetRequiredService<IInboxEventManager>();
            var namingPolicyType = subscribersInformation.Settings.PropertyNamingPolicy ?? NamingPolicyType.PascalCase;
            // If storing is cancelled by stopping the application, the event is not acknowledged and RabbitMQ redelivers it.
            _ = await inboxEventManager.StoreAsync(eventId, subscribersInformation.EventTypeName,
                EventProviderType.MessageBroker,
                payload: eventPayload,
                headers: eventHeadersAsJson,
                eventPath: eventArgs.RoutingKey,
                namingPolicyType: namingPolicyType,
                cancellationToken: _serviceCancellationToken);
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