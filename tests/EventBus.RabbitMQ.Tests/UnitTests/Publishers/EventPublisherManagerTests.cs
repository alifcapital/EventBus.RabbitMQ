using System.Collections.Concurrent;
using System.Reflection;
using EventBus.RabbitMQ.Configurations;
using EventBus.RabbitMQ.Publishers.Managers;
using EventBus.RabbitMQ.Publishers.Models;
using EventBus.RabbitMQ.Publishers.Options;
using EventBus.RabbitMQ.Tests.Domain;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RabbitMQ.Client;

namespace EventBus.RabbitMQ.Tests.UnitTests.Publishers;

public class EventPublisherManagerTests : BaseTestEntity
{
    private IEventPublisherCollector _publisherCollector;
    private EventPublisherManager _publisherManager;

    #region SetUp

    [SetUp]
    public void Setup()
    {
        var logger = Substitute.For<ILogger<EventPublisherManager>>();
        _publisherCollector = Substitute.For<IEventPublisherCollector>();
        _publisherManager = new EventPublisherManager(logger, _publisherCollector);
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
        var publishEvent = new SimplePublishEvent();
        var eventSettings = new EventPublisherOptions();
        var virtualHostSettings = new RabbitMqHostSettings()
        {
            VirtualHost = "TestVirtualHost",
            ExchangeName = "TestExchangeName"
        };
        eventSettings.SetVirtualHostAndUnassignedSettings(virtualHostSettings, publishEvent.GetType().Name);
        _publisherCollector.GetPublisherSettings(publishEvent).Returns(eventSettings);
        var channel = Substitute.For<IModel>();
        _publisherCollector.CreateRabbitMqChannel(eventSettings).Returns(channel);

        await _publisherManager.PublishAsync(publishEvent);

        _publisherCollector.Received(1).GetPublisherSettings(publishEvent);
        _publisherCollector.Received(1).CreateRabbitMqChannel(eventSettings);
        channel.Received(1).BasicPublish(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(),
            Arg.Any<IBasicProperties>(), Arg.Any<ReadOnlyMemory<byte>>());
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
        _publisherCollector.DidNotReceive().CreateRabbitMqChannel(Arg.Any<EventPublisherOptions>());
    }

    [Test]
    public void Dispose_ThereIsOneCollectedEvent_ShouldBePublishedOneEvent()
    {
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
        var channel = Substitute.For<IModel>();
        _publisherCollector.CreateRabbitMqChannel(eventSettings).Returns(channel);

        _publisherManager.Dispose();

        _publisherCollector.Received(1).GetPublisherSettings(publishEvent);
        _publisherCollector.Received(1).CreateRabbitMqChannel(eventSettings);
        channel.Received(1).BasicPublish(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(),
            Arg.Any<IBasicProperties>(), Arg.Any<ReadOnlyMemory<byte>>());
    }

    [Test]
    public void Dispose_ThereAreTwoCollectedEvents_ShouldBePublishedTwoEvents()
    {
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
        var channel = Substitute.For<IModel>();
        _publisherCollector.CreateRabbitMqChannel(eventSettings).Returns(channel);

        _publisherManager.Dispose();

        _publisherCollector.Received(2).GetPublisherSettings(Arg.Any<IPublishEvent>());
        _publisherCollector.Received(2).CreateRabbitMqChannel(eventSettings);
        channel.Received(2).BasicPublish(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(),
            Arg.Any<IBasicProperties>(), Arg.Any<ReadOnlyMemory<byte>>());
    }

    #endregion

    #region Helper methods

    private static readonly FieldInfo EventsToPublishFieldInfo = typeof(EventPublisherManager).GetField(
        "_eventsToPublish", BindingFlags.NonPublic | BindingFlags.Instance);

    private ICollection<IPublishEvent> GetCollectedEvents()
    {
        var eventsToSend =
            EventsToPublishFieldInfo!.GetValue(_publisherManager) as ConcurrentDictionary<Guid, IPublishEvent>;
        return eventsToSend!.Values;
    }

    #endregion
}