using System.Collections.Concurrent;
using System.Reflection;
using EventBus.RabbitMQ.Configurations;
using EventBus.RabbitMQ.Exceptions;
using EventBus.RabbitMQ.Publishers.Managers;
using EventBus.RabbitMQ.Publishers.Models;
using EventBus.RabbitMQ.Publishers.Options;
using EventBus.RabbitMQ.Tests.Domain;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace EventBus.RabbitMQ.Tests.UnitTests.Publishers;

public class EventPublisherManagerTests : BaseTestEntity
{
    private ILogger<EventPublisherManager> _logger;
    private IEventPublisherCollector _publisherCollector;
    private EventPublisherManager _publisherManager;

    #region SetUp

    [SetUp]
    public void Setup()
    {
        _logger = Substitute.For<ILogger<EventPublisherManager>>();
        _publisherCollector = Substitute.For<IEventPublisherCollector>();
        _publisherManager = new EventPublisherManager(_logger, _publisherCollector);
    }

    [TearDown]
    public void TearDown()
    {
        _publisherManager.Dispose();
    }

    #endregion

    #region PublishAsync

    [Test]
    public async Task PublishAsync_PublishingOneEvent_ShouldBePublishedOneEvent()
    {
        var cancellationToken = CancellationToken.None;
        var publishEvent = new SimplePublishEvent();
        var eventSettings = new EventPublisherOptions();
        var virtualHostSettings = new RabbitMqHostSettings()
        {
            VirtualHost = "TestVirtualHost",
            ExchangeName = "TestExchangeName"
        };
        eventSettings.SetVirtualHostAndUnassignedSettings(virtualHostSettings, publishEvent.GetType().Name);
        _publisherCollector.GetPublisherSettings(publishEvent).Returns(eventSettings);
        var channel = Substitute.For<IChannel>();
        _publisherCollector.CreateRabbitMqChannelAsync(eventSettings, cancellationToken).Returns(Task.FromResult(channel));

        await _publisherManager.PublishAsync(publishEvent, cancellationToken);

        _publisherCollector.Received(1).GetPublisherSettings(publishEvent);
        await _publisherCollector.Received(1).CreateRabbitMqChannelAsync(eventSettings, cancellationToken);
        await channel.Received(1).BasicPublishAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(),
            Arg.Any<BasicProperties>(), Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task PublishAsync_PublishingOneEvent_EventShouldBePublishedAsPersistent()
    {
        var cancellationToken = CancellationToken.None;
        var publishEvent = new SimplePublishEvent();
        var eventSettings = new EventPublisherOptions();
        var virtualHostSettings = new RabbitMqHostSettings()
        {
            VirtualHost = "TestVirtualHost",
            ExchangeName = "TestExchangeName"
        };
        eventSettings.SetVirtualHostAndUnassignedSettings(virtualHostSettings, publishEvent.GetType().Name);
        _publisherCollector.GetPublisherSettings(publishEvent).Returns(eventSettings);
        var channel = Substitute.For<IChannel>();
        _publisherCollector.CreateRabbitMqChannelAsync(eventSettings, cancellationToken).Returns(Task.FromResult(channel));

        await _publisherManager.PublishAsync(publishEvent, cancellationToken);

        await channel.Received(1).BasicPublishAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(),
            Arg.Is<BasicProperties>(properties => properties.DeliveryMode == DeliveryModes.Persistent),
            Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task PublishAsync_PublishingOneEvent_ShouldDisposeTheCreatedChannel()
    {
        var cancellationToken = CancellationToken.None;
        var publishEvent = new SimplePublishEvent();
        var channel = SetupPublishingChannel(publishEvent, cancellationToken);

        await _publisherManager.PublishAsync(publishEvent, cancellationToken);

        _ = channel.Received(1).DisposeAsync();
    }

    [Test]
    public void PublishAsync_WhenBrokerDoesNotAcknowledgeThePublishedEvent_ShouldThrowThePublishException()
    {
        var cancellationToken = CancellationToken.None;
        var publishEvent = new SimplePublishEvent();
        var channel = SetupPublishingChannel(publishEvent, cancellationToken);
        var publishException = new PublishException(publishSequenceNumber: 1, isReturn: false);
        channel.BasicPublishAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<BasicProperties>(),
                Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromException(publishException));

        var exception = Assert.ThrowsAsync<PublishException>(async () =>
            await _publisherManager.PublishAsync(publishEvent, cancellationToken));

        Assert.That(exception, Is.SameAs(publishException));
    }

    [Test]
    public void PublishAsync_WhenBrokerDoesNotAcknowledgeThePublishedEvent_ShouldDisposeTheCreatedChannel()
    {
        var cancellationToken = CancellationToken.None;
        var publishEvent = new SimplePublishEvent();
        var channel = SetupPublishingChannel(publishEvent, cancellationToken);
        channel.BasicPublishAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<BasicProperties>(),
                Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromException(new PublishException(publishSequenceNumber: 1, isReturn: false)));

        Assert.ThrowsAsync<PublishException>(async () =>
            await _publisherManager.PublishAsync(publishEvent, cancellationToken));

        _ = channel.Received(1).DisposeAsync();
    }

    [Test]
    public void PublishAsync_WhenTheChannelCannotBeCreated_ShouldThrowTheException()
    {
        var cancellationToken = CancellationToken.None;
        var publishEvent = new SimplePublishEvent();
        var eventSettings = CreateEventSettings(publishEvent);
        _publisherCollector.GetPublisherSettings(publishEvent).Returns(eventSettings);
        _publisherCollector.CreateRabbitMqChannelAsync(eventSettings, cancellationToken)
            .Returns(Task.FromException<IChannel>(new EventBusException("Connection is not opened.")));

        Assert.ThrowsAsync<EventBusException>(async () =>
            await _publisherManager.PublishAsync(publishEvent, cancellationToken));
    }

    #endregion

    #region Collect

    [Test]
    public void Collect_CollectingEvent_EventShouldBeCollected()
    {
        var logger = Substitute.For<ILogger<EventPublisherManager>>();
        _publisherManager = new EventPublisherManager(logger);
        var publishEvent = new SimplePublishEvent();

        _publisherManager.Collect(publishEvent);

        var collectedEvents = GetCollectedEvents();
        Assert.That(collectedEvents, Has.Count.EqualTo(1));
        Assert.That(collectedEvents, Does.Contain(publishEvent));
    }

    [Test]
    public void Collect_CollectingSingleEventTwice_EventShouldBeCollectedOnce()
    {
        var logger = Substitute.For<ILogger<EventPublisherManager>>();
        _publisherManager = new EventPublisherManager(logger);
        var publishEvent = new SimplePublishEvent();

        _publisherManager.Collect(publishEvent);
        _publisherManager.Collect(publishEvent);

        var collectedEvents = GetCollectedEvents();
        Assert.That(collectedEvents, Has.Count.EqualTo(1));
        Assert.That(collectedEvents, Does.Contain(publishEvent));
    }

    #endregion

    #region CleanCollectedEvents

    [Test]
    public void CleanCollectedEvents_CleaningCollectedEvent_ShouldNotBeEnyItemAfterClean()
    {
        var publishEvent = new SimplePublishEvent();
        _publisherManager.Collect(publishEvent);

        _publisherManager.CleanCollectedEvents();

        var collectedEvents = GetCollectedEvents();
        Assert.That(collectedEvents, Is.Empty);
    }

    #endregion

    #region Dispose

    [Test]
    public void Dispose_ThereIsNoCollectedEvent_ShouldNotBePublishedAnyItem()
    {
        _publisherManager.Dispose();

        _publisherCollector.DidNotReceive().GetPublisherSettings(Arg.Any<IPublishEvent>());
    }

    [Test]
    public void Dispose_ThereIsOneCollectedEvent_ShouldBePublishedOneEvent()
    {
        var cancellationToken = CancellationToken.None;
        var publishEvent = new SimplePublishEvent();
        _publisherManager.Collect(publishEvent);
        var eventSettings = new EventPublisherOptions();
        var virtualHostSettings = new RabbitMqHostSettings()
        {
            VirtualHost = "TestVirtualHost",
            ExchangeName = "TestExchangeName"
        };
        eventSettings.SetVirtualHostAndUnassignedSettings(virtualHostSettings, publishEvent.GetType().Name);
        _publisherCollector.GetPublisherSettings(publishEvent).Returns(eventSettings);
        var channel = Substitute.For<IChannel>();
        _publisherCollector.CreateRabbitMqChannelAsync(eventSettings, cancellationToken).Returns(Task.FromResult(channel));

        _publisherManager.Dispose();

        _publisherCollector.Received(1).GetPublisherSettings(publishEvent);
        _publisherCollector.Received(1).CreateRabbitMqChannelAsync(eventSettings, cancellationToken);
        channel.Received(1).BasicPublishAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(),
            Arg.Any<BasicProperties>(), Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public void Dispose_ThereAreTwoCollectedEvents_ShouldBePublishedTwoEvents()
    {
        var cancellationToken = CancellationToken.None;
        _publisherManager.Collect(new SimplePublishEvent());
        _publisherManager.Collect(new SimplePublishEvent());
        var eventSettings = new EventPublisherOptions();
        var virtualHostSettings = new RabbitMqHostSettings
        {
            VirtualHost = "TestVirtualHost",
            ExchangeName = "TestExchangeName"
        };
        eventSettings.SetVirtualHostAndUnassignedSettings(virtualHostSettings, nameof(SimplePublishEvent));
        _publisherCollector.GetPublisherSettings(Arg.Any<IPublishEvent>()).Returns(eventSettings);
        var channel = Substitute.For<IChannel>();
        _publisherCollector.CreateRabbitMqChannelAsync(eventSettings, cancellationToken).Returns(Task.FromResult(channel));

        _publisherManager.Dispose();

        _publisherCollector.Received(2).GetPublisherSettings(Arg.Any<IPublishEvent>());
        _publisherCollector.Received(2).CreateRabbitMqChannelAsync(eventSettings, Arg.Any<CancellationToken>());
        channel.Received(2).BasicPublishAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(),
            Arg.Any<BasicProperties>(), Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public void Dispose_ThereIsOneCollectedEvent_CollectedEventShouldBePublishedAsPersistent()
    {
        var cancellationToken = CancellationToken.None;
        var publishEvent = new SimplePublishEvent();
        var channel = SetupPublishingChannel(publishEvent, cancellationToken);
        _publisherManager.Collect(publishEvent);

        _publisherManager.Dispose();

        _ = channel.Received(1).BasicPublishAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(),
            Arg.Is<BasicProperties>(properties => properties.DeliveryMode == DeliveryModes.Persistent),
            Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public void Dispose_WhenTheBrokerDoesNotAcknowledgeTheCollectedEvent_ShouldThrowAndKeepTheEventCollected()
    {
        var cancellationToken = CancellationToken.None;
        var publishEvent = new SimplePublishEvent();
        var channel = SetupPublishingChannel(publishEvent, cancellationToken);
        channel.BasicPublishAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<BasicProperties>(),
                Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromException(new PublishException(publishSequenceNumber: 1, isReturn: false)));
        _publisherManager.Collect(publishEvent);

        Assert.Throws<PublishException>(() => _publisherManager.Dispose());

        // The manager is not marked as disposed, so the not published event is kept and published again on the next disposing.
        Assert.That(GetCollectedEvents(), Does.Contain(publishEvent));

        _publisherManager.CleanCollectedEvents();
    }

    #endregion

    #region Finalize

    /// <summary>
    /// Enabling the publisher confirmation makes publishing an event able to fail with a <see cref="PublishException"/>
    /// when the broker does not acknowledge it. Since <see cref="EventPublisherManager"/> publishes the collected
    /// events while it is being disposed, that failure must not reach the finalizer thread, where an unhandled
    /// exception terminates the whole application.
    /// </summary>
    [Test]
    public void Finalize_WhenTheBrokerDoesNotAcknowledgeTheCollectedEvent_ShouldNotThrowFromTheFinalizer()
    {
        var publishEvent = new SimplePublishEvent();
        var channel = SetupPublishingChannel(publishEvent, CancellationToken.None);
        channel.BasicPublishAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<BasicProperties>(),
                Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromException(new PublishException(publishSequenceNumber: 1, isReturn: false)));
        _publisherManager.Collect(publishEvent);

        Assert.DoesNotThrow(() => InvokeFinalizer(),
            "An exception thrown on the finalizer thread terminates the whole application.");

        // The finalizer swallows the failure, so the event is still collected; clear it so TearDown's
        // Dispose() call does not try (and fail) to publish it again with the same throwing channel.
        _publisherManager.CleanCollectedEvents();
    }

    [Test]
    public void Finalize_WhenTheBrokerDoesNotAcknowledgeTheCollectedEvent_ShouldLogTheFailure()
    {
        var publishEvent = new SimplePublishEvent();
        var channel = SetupPublishingChannel(publishEvent, CancellationToken.None);
        channel.BasicPublishAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<BasicProperties>(),
                Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromException(new PublishException(publishSequenceNumber: 1, isReturn: false)));
        _publisherManager.Collect(publishEvent);

        InvokeFinalizer();

        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString().Contains("on finalizing the publisher")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception, string>>());

        _publisherManager.CleanCollectedEvents();
    }

    [Test]
    public void Finalize_WhenThereIsACollectedEvent_ShouldStillPublishIt()
    {
        var publishEvent = new SimplePublishEvent();
        var channel = SetupPublishingChannel(publishEvent, CancellationToken.None);
        _publisherManager.Collect(publishEvent);

        InvokeFinalizer();

        _ = channel.Received(1).BasicPublishAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(),
            Arg.Any<BasicProperties>(), Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>());
    }

    #endregion

    #region Helper methods

    private static EventPublisherOptions CreateEventSettings<TPublishEvent>(TPublishEvent publishEvent)
        where TPublishEvent : class, IPublishEvent
    {
        var eventSettings = new EventPublisherOptions();
        var virtualHostSettings = new RabbitMqHostSettings
        {
            VirtualHost = "TestVirtualHost",
            ExchangeName = "TestExchangeName"
        };
        eventSettings.SetVirtualHostAndUnassignedSettings(virtualHostSettings, publishEvent.GetType().Name);

        return eventSettings;
    }

    /// <summary>
    /// Registers the settings of the given event and returns the channel which is created while publishing that event.
    /// </summary>
    private IChannel SetupPublishingChannel<TPublishEvent>(TPublishEvent publishEvent,
        CancellationToken cancellationToken)
        where TPublishEvent : class, IPublishEvent
    {
        var eventSettings = CreateEventSettings(publishEvent);
        _publisherCollector.GetPublisherSettings(publishEvent).Returns(eventSettings);
        _publisherCollector.GetPublisherSettings<IPublishEvent>(publishEvent).Returns(eventSettings);
        var channel = Substitute.For<IChannel>();
        _publisherCollector.CreateRabbitMqChannelAsync(eventSettings, cancellationToken)
            .Returns(Task.FromResult(channel));

        return channel;
    }

    private static readonly FieldInfo EventsToPublishFieldInfo = typeof(EventPublisherManager).GetField(
        "_eventsToPublish", BindingFlags.NonPublic | BindingFlags.Instance);

    private ICollection<IPublishEvent> GetCollectedEvents()
    {
        var eventsToSend =
            EventsToPublishFieldInfo!.GetValue(_publisherManager) as ConcurrentDictionary<Guid, IPublishEvent>;
        return eventsToSend!.Values;
    }

    /// <summary>
    /// Invokes the finalizer of the manager the same way the garbage collector does.
    /// </summary>
    private void InvokeFinalizer()
    {
        var finalizer = typeof(EventPublisherManager)
            .GetMethod("Finalize", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(finalizer, Is.Not.Null);

        try
        {
            finalizer!.Invoke(_publisherManager, []);
        }
        catch (TargetInvocationException e)
        {
            throw e.InnerException!;
        }
    }

    #endregion
}