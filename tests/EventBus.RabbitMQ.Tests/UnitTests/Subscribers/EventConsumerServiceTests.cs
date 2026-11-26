using System.Reflection;
using System.Text;
using EventBus.RabbitMQ.Configurations;
using EventBus.RabbitMQ.Connections;
using EventBus.RabbitMQ.Subscribers.Consumers;
using EventBus.RabbitMQ.Subscribers.Models;
using EventBus.RabbitMQ.Subscribers.Options;
using EventBus.RabbitMQ.Tests.Domain;
using EventStorage.Inbox.Managers;
using EventStorage.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.Core;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace EventBus.RabbitMQ.Tests.UnitTests.Subscribers;

public class EventConsumerServiceTests : BaseTestEntity
{
    private const string ConsumerTag = "consumer-tag";
    private const ulong DeliveryTag = 7;

    private IServiceProvider _serviceProvider;
    private EventConsumerService _consumerService;
    private ILogger<EventConsumerService> _logger;
    private IRabbitMqConnectionManager _rabbitMqConnectionManager;
    private IRabbitMqConnection _connection;
    private EventSubscriberOptions _settings;

    #region SetUp

    [SetUp]
    public void Setup()
    {
        var rabbitMqOptions = RabbitMqOptionsConstant.CreateDefaultRabbitMqOptions();
        _settings = new EventSubscriberOptions
        {
            EventTypeName = nameof(SimpleSubscribeEvent),
            QueueName = "test-queue",
        };
        _settings.SetVirtualHostAndUnassignedSettings(rabbitMqOptions, _settings.EventTypeName);

        _serviceProvider = Substitute.For<IServiceProvider>();
        _logger = Substitute.For<ILogger<EventConsumerService>>();
        _serviceProvider.GetService(typeof(ILogger<EventConsumerService>)).Returns(_logger);

        _rabbitMqConnectionManager = Substitute.For<IRabbitMqConnectionManager>();
        _serviceProvider.GetService(typeof(IRabbitMqConnectionManager)).Returns(_rabbitMqConnectionManager);

        _consumerService = new EventConsumerService(_settings, _serviceProvider, false);
    }

    #endregion

    #region AddSubscriber

    [Test]
    public void AddSubscriber_WithOptionsQueueName_ShouldAddSubscriber()
    {
        var queueName = "test-queue";
        var eventType = typeof(SimpleSubscribeEvent);
        var subscriberType = typeof(SimpleEventSubscriberHandler);
        var settings = new EventSubscriberOptions
        {
            EventTypeName = eventType.Name,
            QueueName = queueName,
        };
        var subscribersInformation = new SubscribersInformation
        {
            EventTypeName = eventType.Name,
            Settings = settings
        };
        subscribersInformation.AddSubscriberIfNotExists(eventType, subscriberType);

        _consumerService.AddSubscriber(subscribersInformation);

        var allSubscribers = GetAllSubscribersInformation();
        Assert.That(allSubscribers.ContainsKey(eventType.Name), Is.True);

        var subscribersInfo = allSubscribers[eventType.Name];
        Assert.That(subscribersInfo.Settings.QueueName, Is.EqualTo(queueName));
        Assert.That(subscribersInfo.Subscribers.Count, Is.EqualTo(1));
    }

    #endregion

    #region StartAndSubscribeReceiverAsync

    [Test]
    public async Task StartAndSubscribeReceiverAsync_StartingConsumerWithDefaultSetting_ShouldCreateConsumer()
    {
        var cancellationToken = CancellationToken.None;
        var channel = SetupConsumerChannel();

        await _consumerService.CreateChannelAndSubscribeReceiverAsync(cancellationToken);

        var consumerChannel = GetConsumerChannel(_consumerService);
        _rabbitMqConnectionManager.Received().GetOrCreateConnection(_settings.VirtualHostSettings);
        Assert.That(consumerChannel, Is.SameAs(channel));
    }

    [Test]
    public async Task StartAndSubscribeReceiverAsync_WhenCreatingChannelFailsWhileStopping_ShouldNotLogError()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        var connection = Substitute.For<IRabbitMqConnection>();
        connection.CreateConsumerChannelAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                cancellationTokenSource.Cancel();
                return Task.FromException<IChannel>(new ObjectDisposedException(nameof(IServiceProvider)));
            });
        _rabbitMqConnectionManager.GetOrCreateConnection(_settings.VirtualHostSettings).Returns(connection);
        _consumerService = new EventConsumerService(_settings, _serviceProvider, false);

        await _consumerService.CreateChannelAndSubscribeReceiverAsync(cancellationTokenSource.Token);

        Assert.That(HasLog(LogLevel.Error), Is.False);
    }

    [Test]
    public async Task StartAndSubscribeReceiverAsync_WhenCreatingChannelFailsAfterStoppingStarted_ShouldNotLogError()
    {
        var connection = Substitute.For<IRabbitMqConnection>();
        connection.CreateConsumerChannelAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IChannel>(new ObjectDisposedException(nameof(IServiceProvider))));
        _rabbitMqConnectionManager.GetOrCreateConnection(_settings.VirtualHostSettings).Returns(connection);
        _consumerService = new EventConsumerService(_settings, _serviceProvider, false);
        await _consumerService.StopReceivingEventsAsync(CancellationToken.None);

        await _consumerService.CreateChannelAndSubscribeReceiverAsync(CancellationToken.None);

        Assert.That(HasLog(LogLevel.Error), Is.False);
    }

    [Test]
    public async Task StartAndSubscribeReceiverAsync_WhenCreatingChannelFailsWithoutStopping_ShouldLogError()
    {
        var connection = Substitute.For<IRabbitMqConnection>();
        connection.CreateConsumerChannelAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IChannel>(new InvalidOperationException("Broker is unreachable")));
        _rabbitMqConnectionManager.GetOrCreateConnection(_settings.VirtualHostSettings).Returns(connection);
        _consumerService = new EventConsumerService(_settings, _serviceProvider, false);
        using var cancellationTokenSource = new CancellationTokenSource();

        await _consumerService.CreateChannelAndSubscribeReceiverAsync(cancellationTokenSource.Token);

        // Stops the background retry loop which is started after the failure.
        await cancellationTokenSource.CancelAsync();
        Assert.That(HasLog(LogLevel.Error), Is.True);
    }

    #endregion

    #region StopReceivingEventsAsync

    [Test]
    public async Task StopReceivingEventsAsync_AfterStartingConsumer_ShouldCancelConsumerAndCloseChannel()
    {
        var channel = SetupConsumerChannel();
        await _consumerService.CreateChannelAndSubscribeReceiverAsync(CancellationToken.None);

        await _consumerService.StopReceivingEventsAsync(CancellationToken.None);

        await channel.Received(1).BasicCancelAsync(ConsumerTag, false, Arg.Any<CancellationToken>());
        Assert.That(channel.ReceivedCalls().Any(call => call.GetMethodInfo().Name == nameof(IChannel.CloseAsync)),
            Is.True);
    }

    [Test]
    public async Task StopReceivingEventsAsync_BeforeStartingConsumer_ShouldNotThrow()
    {
        await Assert.DoesNotThrowAsync(() => _consumerService.StopReceivingEventsAsync(CancellationToken.None));
    }

    [Test]
    public async Task StopReceivingEventsAsync_WhenCancellingConsumerFails_ShouldStillCloseChannel()
    {
        var channel = SetupConsumerChannel();
        channel.BasicCancelAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Channel is broken")));
        await _consumerService.CreateChannelAndSubscribeReceiverAsync(CancellationToken.None);

        await _consumerService.StopReceivingEventsAsync(CancellationToken.None);

        Assert.That(channel.ReceivedCalls().Any(call => call.GetMethodInfo().Name == nameof(IChannel.CloseAsync)),
            Is.True);
    }

    [Test]
    public async Task StopReceivingEventsAsync_WhenChannelIsAlreadyClosed_ShouldNotCancelConsumerNorCloseChannel()
    {
        var channel = SetupConsumerChannel();
        await _consumerService.CreateChannelAndSubscribeReceiverAsync(CancellationToken.None);
        channel.IsOpen.Returns(false);

        await _consumerService.StopReceivingEventsAsync(CancellationToken.None);

        await channel.DidNotReceive()
            .BasicCancelAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
        Assert.That(channel.ReceivedCalls().Any(call => call.GetMethodInfo().Name == nameof(IChannel.CloseAsync)),
            Is.False);
    }

    [Test]
    public async Task StopReceivingEventsAsync_WhenChannelBelongsToLostConnection_ShouldNotCancelConsumerNorCloseChannel()
    {
        var channel = SetupConsumerChannel();
        await _consumerService.CreateChannelAndSubscribeReceiverAsync(CancellationToken.None);
        // The channel still reports open, but its connection was replaced.
        _connection.ConnectionId.Returns(Guid.CreateVersion7());

        await _consumerService.StopReceivingEventsAsync(CancellationToken.None);

        await channel.DidNotReceive()
            .BasicCancelAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
        Assert.That(channel.ReceivedCalls().Any(call => call.GetMethodInfo().Name == nameof(IChannel.CloseAsync)),
            Is.False);
        Assert.That(HasLog(LogLevel.Warning), Is.False);
    }

    [Test]
    public async Task StopReceivingEventsAsync_WhenCancellingConsumerThrowsAlreadyClosedException_ShouldNotLogWarning()
    {
        var channel = SetupConsumerChannel();
        var shutdownReason = new ShutdownEventArgs(ShutdownInitiator.Peer, 320, "CONNECTION_FORCED");
        channel.BasicCancelAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new AlreadyClosedException(shutdownReason)));
        await _consumerService.CreateChannelAndSubscribeReceiverAsync(CancellationToken.None);

        await _consumerService.StopReceivingEventsAsync(CancellationToken.None);

        Assert.That(HasLog(LogLevel.Warning), Is.False);
    }

    [Test]
    public async Task StopReceivingEventsAsync_AfterStartingConsumer_ShouldUnsubscribeFromReconnected()
    {
        SetupConsumerChannel();
        await _consumerService.CreateChannelAndSubscribeReceiverAsync(CancellationToken.None);

        await _consumerService.StopReceivingEventsAsync(CancellationToken.None);

        _connection.Received(1).Reconnected -= Arg.Any<EventHandler>();
    }

    [Test]
    public async Task StopReceivingEventsAsync_WhenClosingChannelFails_ShouldNotThrow()
    {
        var channel = SetupConsumerChannel();
        channel.CloseAsync(Arg.Any<ushort>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Channel is broken")));
        await _consumerService.CreateChannelAndSubscribeReceiverAsync(CancellationToken.None);

        await Assert.DoesNotThrowAsync(() => _consumerService.StopReceivingEventsAsync(CancellationToken.None));
    }

    [Test]
    public async Task StopReceivingEventsAsync_WhileEventIsBeingHandled_ShouldWaitUntilItIsHandledBeforeClosingChannel()
    {
        var (consumerService, channel, storing) = await StartInboxConsumerWithPendingStoringAsync();
        var receivingTask = InvokeReceivingEventAsync(consumerService);

        var stoppingTask = consumerService.StopReceivingEventsAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromMilliseconds(300));
        var isStoppedBeforeEventIsHandled = stoppingTask.IsCompleted;
        storing.SetResult(true);
        await receivingTask;
        await stoppingTask;

        Assert.That(isStoppedBeforeEventIsHandled, Is.False);
        await channel.Received(1).BasicAckAsync(DeliveryTag, false, Arg.Any<CancellationToken>());
        Assert.That(channel.ReceivedCalls().Any(call => call.GetMethodInfo().Name == nameof(IChannel.CloseAsync)),
            Is.True);
    }

    [Test]
    public async Task StopReceivingEventsAsync_WhenWaitingForHandlingEventIsCancelled_ShouldLogWarningAndCloseChannel()
    {
        var (consumerService, channel, storing) = await StartInboxConsumerWithPendingStoringAsync();
        var receivingTask = InvokeReceivingEventAsync(consumerService);
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        await consumerService.StopReceivingEventsAsync(cancellationTokenSource.Token);

        Assert.That(HasLog(LogLevel.Warning), Is.True);
        Assert.That(channel.ReceivedCalls().Any(call => call.GetMethodInfo().Name == nameof(IChannel.CloseAsync)),
            Is.True);
        storing.SetResult(true);
        await receivingTask;
    }

    #endregion

    #region OnChannelShutdownAsync

    [Test]
    public async Task OnChannelShutdownAsync_AfterStopping_ShouldNotRecreateChannel()
    {
        SetupConsumerChannel();
        await _consumerService.CreateChannelAndSubscribeReceiverAsync(CancellationToken.None);
        await _consumerService.StopReceivingEventsAsync(CancellationToken.None);
        var reason = new ShutdownEventArgs(ShutdownInitiator.Application, 200, "Goodbye");

        await InvokePrivateMethodAsync(_consumerService, "OnChannelShutdownAsync", reason);

        await _connection.Received(1).CreateConsumerChannelAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task OnChannelShutdownAsync_WithoutStopping_ShouldRecreateChannel()
    {
        SetupConsumerChannel();
        await _consumerService.CreateChannelAndSubscribeReceiverAsync(CancellationToken.None);
        var reason = new ShutdownEventArgs(ShutdownInitiator.Peer, 320, "Connection forced");

        await InvokePrivateMethodAsync(_consumerService, "OnChannelShutdownAsync", reason);

        // The channel is recreated in the background.
        await WaitUntilAsync(() => CountCreatedConsumerChannels() == 2);
        Assert.That(CountCreatedConsumerChannels(), Is.EqualTo(2));
    }

    [Test]
    public async Task OnChannelShutdownAsync_WithoutStopping_ShouldDisposeOldChannelBeforeCreatingNewOne()
    {
        var channels = SetupConsumerChannelsCreatingNewChannelEachTime();
        await _consumerService.CreateChannelAndSubscribeReceiverAsync(CancellationToken.None);
        var oldChannel = channels.Single();
        var reason = new ShutdownEventArgs(ShutdownInitiator.Peer, 320, "Connection forced");

        await InvokePrivateMethodAsync(_consumerService, "OnChannelShutdownAsync", reason);

        await WaitUntilAsync(() => channels.Count == 2 && GetConsumerChannel(_consumerService) == channels[1]);
        oldChannel.Received(1).Dispose();
        Assert.That(GetConsumerChannel(_consumerService), Is.SameAs(channels[1]));
        Assert.That(HasLogContaining(LogLevel.Warning, "Recreating RabbitMQ consumer channel"), Is.True);
    }

    [Test]
    public async Task OnChannelShutdownAsync_WhenRaisedTogetherWithReconnected_ShouldCreateOnlyOneNewConsumer()
    {
        var channels = SetupConsumerChannelsCreatingNewChannelEachTime();
        await _consumerService.CreateChannelAndSubscribeReceiverAsync(CancellationToken.None);
        _connection.ConnectionId.Returns(Guid.CreateVersion7());
        var reason = new ShutdownEventArgs(ShutdownInitiator.Peer, 320, "Connection forced");

        await Task.WhenAll(
            InvokePrivateMethodAsync(_consumerService, "OnChannelShutdownAsync", reason),
            Task.Run(() => _connection.Reconnected += Raise.Event()));

        await WaitUntilAsync(() => channels.Count >= 2);
        // Let the duplicate request finish before asserting it created nothing.
        await Task.Delay(TimeSpan.FromMilliseconds(300));
        Assert.That(channels, Has.Count.EqualTo(2));
    }

    #endregion

    #region OnConnectionReconnected

    [Test]
    public async Task OnConnectionReconnected_WhenChannelBelongsToLostConnection_ShouldRecreateChannel()
    {
        var channels = SetupConsumerChannelsCreatingNewChannelEachTime();
        await _consumerService.CreateChannelAndSubscribeReceiverAsync(CancellationToken.None);
        // A channel that missed the shutdown notification still reports open.
        _connection.ConnectionId.Returns(Guid.CreateVersion7());

        _connection.Reconnected += Raise.Event();

        await WaitUntilAsync(() => channels.Count == 2 && GetConsumerChannel(_consumerService) == channels[1]);
        channels[0].Received(1).Dispose();
        Assert.That(GetConsumerChannel(_consumerService), Is.SameAs(channels[1]));
    }

    [Test]
    public async Task OnConnectionReconnected_WhenChannelIsOpenOnCurrentConnection_ShouldNotRecreateChannel()
    {
        var channels = SetupConsumerChannelsCreatingNewChannelEachTime();
        await _consumerService.CreateChannelAndSubscribeReceiverAsync(CancellationToken.None);

        _connection.Reconnected += Raise.Event();

        await Task.Delay(TimeSpan.FromMilliseconds(300));
        Assert.That(channels, Has.Count.EqualTo(1));
        channels[0].DidNotReceive().Dispose();
    }

    [Test]
    public async Task OnConnectionReconnected_AfterStopping_ShouldNotRecreateChannel()
    {
        var channels = SetupConsumerChannelsCreatingNewChannelEachTime();
        await _consumerService.CreateChannelAndSubscribeReceiverAsync(CancellationToken.None);
        await _consumerService.StopReceivingEventsAsync(CancellationToken.None);
        _connection.ConnectionId.Returns(Guid.CreateVersion7());

        _connection.Reconnected += Raise.Event();

        await Task.Delay(TimeSpan.FromMilliseconds(300));
        Assert.That(channels, Has.Count.EqualTo(1));
    }

    #endregion

    #region OnCallbackExceptionAsync

    [Test]
    public async Task OnCallbackExceptionAsync_AfterStopping_ShouldNotRecreateChannel()
    {
        SetupConsumerChannel();
        await _consumerService.CreateChannelAndSubscribeReceiverAsync(CancellationToken.None);
        await _consumerService.StopReceivingEventsAsync(CancellationToken.None);
        var eventArgs = new CallbackExceptionEventArgs(new Dictionary<string, object>(),
            new InvalidOperationException("Callback failed"));

        await InvokePrivateMethodAsync(_consumerService, "OnCallbackExceptionAsync", eventArgs);

        await _connection.Received(1).CreateConsumerChannelAsync(Arg.Any<CancellationToken>());
    }

    #endregion

    #region Consumer_ReceivingEvent

    [Test]
    public async Task ReceivingEvent_AfterStopping_ShouldNotHandleNorAcknowledgeTheEvent()
    {
        var channel = SetupConsumerChannel();
        AddSimpleSubscriber(_consumerService);
        await _consumerService.CreateChannelAndSubscribeReceiverAsync(CancellationToken.None);
        await _consumerService.StopReceivingEventsAsync(CancellationToken.None);

        await InvokeReceivingEventAsync(_consumerService);

        _serviceProvider.DidNotReceive().GetService(typeof(IServiceScopeFactory));
        await channel.DidNotReceive()
            .BasicAckAsync(Arg.Any<ulong>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// When the application is stopping, the service provider can already be disposed while an event is being
    /// received. That error is caused by the shutdown, so it must not be reported and the event must not be
    /// acknowledged to let RabbitMQ redeliver it.
    /// </summary>
    [Test]
    public async Task ReceivingEvent_WhenServiceProviderIsDisposedWhileStopping_ShouldNotLogErrorNorAcknowledge()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        var channel = SetupConsumerChannel();
        AddSimpleSubscriber(_consumerService);
        await _consumerService.CreateChannelAndSubscribeReceiverAsync(cancellationTokenSource.Token);
        _serviceProvider.GetService(typeof(IServiceScopeFactory))
            .Returns(_ => throw new ObjectDisposedException(nameof(IServiceProvider)));
        await cancellationTokenSource.CancelAsync();

        await InvokeReceivingEventAsync(_consumerService);

        Assert.That(HasLog(LogLevel.Error), Is.False);
        await channel.DidNotReceive()
            .BasicAckAsync(Arg.Any<ulong>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ReceivingEvent_WhenServiceProviderIsDisposedWithoutStopping_ShouldLogError()
    {
        SetupConsumerChannel();
        AddSimpleSubscriber(_consumerService);
        await _consumerService.CreateChannelAndSubscribeReceiverAsync(CancellationToken.None);
        _serviceProvider.GetService(typeof(IServiceScopeFactory))
            .Returns(_ => throw new ObjectDisposedException(nameof(IServiceProvider)));

        await InvokeReceivingEventAsync(_consumerService);

        Assert.That(HasLog(LogLevel.Error), Is.True);
    }

    [Test]
    public async Task ReceivingEvent_WithoutInbox_ShouldHandleEventAndAcknowledgeItWithoutCancellationToken()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        var channel = SetupConsumerChannel();
        AddSimpleSubscriber(_consumerService);
        await _consumerService.CreateChannelAndSubscribeReceiverAsync(cancellationTokenSource.Token);
        var scopedServiceProvider = SetupScopedServiceProvider();
        scopedServiceProvider.GetService(typeof(SimpleEventSubscriberHandler))
            .Returns(new SimpleEventSubscriberHandler());

        await InvokeReceivingEventAsync(_consumerService);

        scopedServiceProvider.Received(1).GetService(typeof(SimpleEventSubscriberHandler));
        await channel.Received(1).BasicAckAsync(DeliveryTag, false, CancellationToken.None);
    }

    [Test]
    public async Task ReceivingEvent_WithInbox_ShouldStoreEventWithServiceTokenAndAcknowledgeIt()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        var channel = SetupConsumerChannel();
        var consumerService = new EventConsumerService(_settings, _serviceProvider, useInbox: true);
        AddSimpleSubscriber(consumerService);
        await consumerService.CreateChannelAndSubscribeReceiverAsync(cancellationTokenSource.Token);
        var inboxEventManager = SetupScopedService<IInboxEventManager>();
        inboxEventManager.StoreAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<EventProviderType>(),
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<NamingPolicyType>(), Arg.Any<CancellationToken>())
            .Returns(true);

        await InvokeReceivingEventAsync(consumerService);

        await inboxEventManager.Received(1).StoreAsync(Arg.Any<Guid>(), nameof(SimpleSubscribeEvent),
            EventProviderType.MessageBroker, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<NamingPolicyType>(), cancellationTokenSource.Token);
        await channel.Received(1).BasicAckAsync(DeliveryTag, false, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ReceivingEvent_WithInboxWhenStoringIsCancelledByStopping_ShouldNotLogErrorNorAcknowledge()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        var channel = SetupConsumerChannel();
        var consumerService = new EventConsumerService(_settings, _serviceProvider, useInbox: true);
        AddSimpleSubscriber(consumerService);
        await consumerService.CreateChannelAndSubscribeReceiverAsync(cancellationTokenSource.Token);
        var inboxEventManager = SetupScopedService<IInboxEventManager>();
        inboxEventManager.StoreAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<EventProviderType>(),
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<NamingPolicyType>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                cancellationTokenSource.Cancel();
                return Task.FromCanceled<bool>(cancellationTokenSource.Token);
            });

        await InvokeReceivingEventAsync(consumerService);

        Assert.That(HasLog(LogLevel.Error), Is.False);
        await channel.DidNotReceive()
            .BasicAckAsync(Arg.Any<ulong>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    #endregion

    #region Helper methods

    /// <summary>
    /// Get the subscribers information from the EventConsumerService
    /// </summary>
    private Dictionary<string, SubscribersInformation> GetAllSubscribersInformation()
    {
        const string subscribersFieldName = "_subscribers";
        var field = _consumerService.GetType()
            .GetField(subscribersFieldName, BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.That(field, Is.Not.Null);

        var subscribers = (Dictionary<string, SubscribersInformation>)field?.GetValue(_consumerService)!;
        return subscribers;
    }

    private static IChannel GetConsumerChannel(EventConsumerService consumerService)
    {
        var field = typeof(EventConsumerService)
            .GetField("_consumerChannel", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null);

        return field!.GetValue(consumerService) as IChannel;
    }

    /// <summary>
    /// Sets up the connection and the consumer channel, so the consumer can be started successfully.
    /// </summary>
    private IChannel SetupConsumerChannel()
    {
        var channel = CreateOpenedConsumerChannel();
        SetupConsumerConnection(_ => channel);

        return channel;
    }

    /// <summary>
    /// Sets up the connection to create a new channel each time, to tell the recreated channel from the old one.
    /// </summary>
    /// <returns>The channels created so far, in the order of creation.</returns>
    private List<IChannel> SetupConsumerChannelsCreatingNewChannelEachTime()
    {
        var channels = new List<IChannel>();
        SetupConsumerConnection(_ =>
        {
            var channel = CreateOpenedConsumerChannel();
            lock (channels)
                channels.Add(channel);

            return channel;
        });

        return channels;
    }

    /// <summary>
    /// Sets up the connection to create channels with the given factory, and creates the consumer.
    /// </summary>
    private void SetupConsumerConnection(Func<CallInfo, IChannel> channelFactory)
    {
        var connection = Substitute.For<IRabbitMqConnection>();
        connection.CreateConsumerChannelAsync(Arg.Any<CancellationToken>())
            .Returns(callInfo => Task.FromResult(channelFactory(callInfo)));
        _rabbitMqConnectionManager.GetOrCreateConnection(_settings.VirtualHostSettings).Returns(connection);
        _connection = connection;
        // The consumer resolves its connection while it is being created, so it is created after the connection.
        _consumerService = new EventConsumerService(_settings, _serviceProvider, false);
    }

    /// <summary>
    /// Creates an opened channel on which the consumer can be started successfully.
    /// </summary>
    private static IChannel CreateOpenedConsumerChannel()
    {
        var channel = Substitute.For<IChannel>();
        channel.IsOpen.Returns(true);
        channel.QueueDeclareAsync(
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<IDictionary<string, object>>(),
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new QueueDeclareOk(queueName: "test-queue", messageCount: 0, consumerCount: 0)));
        channel.BasicConsumeAsync(
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<IDictionary<string, object>>(),
                Arg.Any<IAsyncBasicConsumer>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ConsumerTag));

        return channel;
    }

    /// <summary>
    /// Counts how many consumer channels were requested from the connection.
    /// </summary>
    private int CountCreatedConsumerChannels()
    {
        return _connection.ReceivedCalls()
            .Count(call => call.GetMethodInfo().Name == nameof(IRabbitMqConnection.CreateConsumerChannelAsync));
    }

    /// <summary>
    /// Waits up to 5 seconds for the condition, for work done in the background.
    /// </summary>
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(TimeSpan.FromMilliseconds(20));
    }

    /// <summary>
    /// Sets up the scope factory of the service provider to resolve the given service from the created scope.
    /// </summary>
    private TService SetupScopedService<TService>() where TService : class
    {
        var service = Substitute.For<TService>();
        var scopedServiceProvider = SetupScopedServiceProvider();
        scopedServiceProvider.GetService(typeof(TService)).Returns(service);

        return service;
    }

    /// <summary>
    /// Sets up the scope factory of the service provider and returns the service provider of the created scope.
    /// </summary>
    private IServiceProvider SetupScopedServiceProvider()
    {
        var scopedServiceProvider = Substitute.For<IServiceProvider>();
        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(scopedServiceProvider);
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);
        _serviceProvider.GetService(typeof(IServiceScopeFactory)).Returns(scopeFactory);

        return scopedServiceProvider;
    }

    /// <summary>
    /// Starts an inbox consumer whose storing of the received event does not complete until the returned
    /// completion source is completed, to simulate an event being handled.
    /// </summary>
    private async Task<(EventConsumerService ConsumerService, IChannel Channel, TaskCompletionSource<bool> Storing)>
        StartInboxConsumerWithPendingStoringAsync()
    {
        var channel = SetupConsumerChannel();
        var consumerService = new EventConsumerService(_settings, _serviceProvider, useInbox: true);
        AddSimpleSubscriber(consumerService);
        await consumerService.CreateChannelAndSubscribeReceiverAsync(CancellationToken.None);
        var storing = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var inboxEventManager = SetupScopedService<IInboxEventManager>();
        inboxEventManager.StoreAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<EventProviderType>(),
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<NamingPolicyType>(), Arg.Any<CancellationToken>())
            .Returns(storing.Task);

        return (consumerService, channel, storing);
    }

    private static async Task InvokePrivateMethodAsync(EventConsumerService consumerService, string methodName,
        object eventArgs)
    {
        var method = typeof(EventConsumerService)
            .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(method, Is.Not.Null);

        await (Task)method!.Invoke(consumerService, [null, eventArgs])!;
    }

    private void AddSimpleSubscriber(EventConsumerService consumerService)
    {
        var subscribersInformation = new SubscribersInformation
        {
            EventTypeName = nameof(SimpleSubscribeEvent),
            Settings = _settings
        };
        subscribersInformation.AddSubscriberIfNotExists(typeof(SimpleSubscribeEvent),
            typeof(SimpleEventSubscriberHandler));
        consumerService.AddSubscriber(subscribersInformation);
    }

    /// <summary>
    /// Invokes the handler of the received RabbitMQ events the same way the RabbitMQ consumer does.
    /// </summary>
    private static async Task InvokeReceivingEventAsync(EventConsumerService consumerService)
    {
        var properties = new BasicProperties
        {
            MessageId = Guid.NewGuid().ToString(),
            Type = nameof(SimpleSubscribeEvent)
        };
        var body = Encoding.UTF8.GetBytes("{\"Name\":\"Test\"}");
        var eventArgs = new BasicDeliverEventArgs(ConsumerTag, DeliveryTag, redelivered: false, "exchange",
            "routing-key", properties, body);

        var method = typeof(EventConsumerService)
            .GetMethod("Consumer_ReceivingEvent", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(method, Is.Not.Null);

        await (Task)method!.Invoke(consumerService, [null, eventArgs])!;
    }

    private bool HasLog(LogLevel level)
    {
        return _logger.ReceivedCalls().Any(call =>
            call.GetMethodInfo().Name == nameof(ILogger.Log) &&
            call.GetArguments()[0] is LogLevel logLevel &&
            logLevel == level);
    }

    private bool HasLogContaining(LogLevel level, string message)
    {
        return _logger.ReceivedCalls().Any(call =>
        {
            if (call.GetMethodInfo().Name != nameof(ILogger.Log))
                return false;

            var arguments = call.GetArguments();
            return arguments[0] is LogLevel logLevel && logLevel == level &&
                   arguments[2]?.ToString()?.Contains(message, StringComparison.Ordinal) == true;
        });
    }

    #endregion
}
