using EventBus.RabbitMQ.Information;
using EventBus.RabbitMQ.Information.Models;
using EventBus.RabbitMQ.Publishers.Managers;
using EventBus.RabbitMQ.Subscribers.Managers;
using EventBus.RabbitMQ.Tests.Domain;
using NSubstitute;

namespace EventBus.RabbitMQ.Tests.UnitTests.Information;

public class EventBusConfigurationProviderTests : BaseTestEntity
{
    private IServiceProvider _serviceProvider;
    private EventBusConfigurationProvider _provider;

    #region SetUp

    [SetUp]
    public void Setup()
    {
        _serviceProvider = Substitute.For<IServiceProvider>();
        _provider = new EventBusConfigurationProvider(_serviceProvider);
    }

    #endregion

    #region GetPublishers

    [Test]
    public void GetPublishers_RabbitMqIsDisabledAndCollectorIsNotRegistered_ShouldReturnEmptyCollection()
    {
        _serviceProvider.GetService(typeof(IEventPublisherCollector)).Returns(null);

        var publishers = _provider.GetPublishers();

        Assert.That(publishers, Is.Empty);
    }

    [Test]
    public void GetPublishers_CollectorIsRegistered_ShouldReturnLoadedPublishersOfCollector()
    {
        var loadedPublisher = new LoadedPublisherInformation
        {
            EventType = typeof(SimplePublishEvent),
            EventTypeName = nameof(SimplePublishEvent),
            RoutingKey = "TestRoutingKey",
            VirtualHostSettings = null
        };
        var publisherCollector = Substitute.For<IEventPublisherCollector>();
        publisherCollector.GetLoadedPublishers().Returns([loadedPublisher]);
        _serviceProvider.GetService(typeof(IEventPublisherCollector)).Returns(publisherCollector);

        var publishers = _provider.GetPublishers();

        Assert.That(publishers, Is.EquivalentTo(new[] { loadedPublisher }));
    }

    #endregion

    #region GetSubscribers

    [Test]
    public void GetSubscribers_RabbitMqIsDisabledAndCollectorIsNotRegistered_ShouldReturnEmptyCollection()
    {
        _serviceProvider.GetService(typeof(IEventSubscriberCollector)).Returns(null);

        var subscribers = _provider.GetSubscribers();

        Assert.That(subscribers, Is.Empty);
    }

    [Test]
    public void GetSubscribers_CollectorIsRegistered_ShouldReturnLoadedSubscribersOfCollector()
    {
        var loadedSubscriber = new LoadedSubscriberInformation
        {
            EventTypeName = nameof(SimpleSubscribeEvent),
            RoutingKey = "TestRoutingKey",
            QueueName = "TestQueueName",
            VirtualHostSettings = null,
            Handlers =
            [
                new LoadedEventHandlerInformation
                {
                    EventType = typeof(SimpleSubscribeEvent),
                    HandlerType = typeof(SimpleEventSubscriberHandler)
                }
            ]
        };
        var subscriberCollector = Substitute.For<IEventSubscriberCollector>();
        subscriberCollector.GetLoadedSubscribers().Returns([loadedSubscriber]);
        _serviceProvider.GetService(typeof(IEventSubscriberCollector)).Returns(subscriberCollector);

        var subscribers = _provider.GetSubscribers();

        Assert.That(subscribers, Is.EquivalentTo(new[] { loadedSubscriber }));
    }

    #endregion
}
