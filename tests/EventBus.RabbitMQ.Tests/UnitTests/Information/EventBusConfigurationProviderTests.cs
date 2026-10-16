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
    public void GetPublishers_CollectorIsRegistered_ShouldReturnPublishersInformationOfCollector()
    {
        var publisherInformation = new EventPublisherInformation
        {
            EventType = new EventTypeInformation
            {
                EventTypeName = nameof(SimplePublishEvent),
                EventTypeNamespace = typeof(SimplePublishEvent).Namespace
            },
            EventTypeName = nameof(SimplePublishEvent),
            RoutingKey = "TestRoutingKey",
            VirtualHostSettings = null
        };
        var publisherCollector = Substitute.For<IEventPublisherCollector>();
        publisherCollector.GetPublishersInformation().Returns([publisherInformation]);
        _serviceProvider.GetService(typeof(IEventPublisherCollector)).Returns(publisherCollector);

        var publishers = _provider.GetPublishers();

        Assert.That(publishers, Is.EquivalentTo(new[] { publisherInformation }));
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
    public void GetSubscribers_CollectorIsRegistered_ShouldReturnSubscribersInformationOfCollector()
    {
        var subscriberInformation = new EventSubscriberInformation
        {
            EventTypeName = nameof(SimpleSubscribeEvent),
            RoutingKey = "TestRoutingKey",
            QueueName = "TestQueueName",
            VirtualHostSettings = null,
            Handlers =
            [
                new EventHandlerInformation
                {
                    EventTypeName = nameof(SimpleSubscribeEvent),
                    EventTypeNamespace = typeof(SimpleSubscribeEvent).Namespace,
                    HandlerTypeName = nameof(SimpleEventSubscriberHandler),
                    HandlerTypeNamespace = typeof(SimpleEventSubscriberHandler).Namespace
                }
            ]
        };
        var subscriberCollector = Substitute.For<IEventSubscriberCollector>();
        subscriberCollector.GetSubscribersInformation().Returns([subscriberInformation]);
        _serviceProvider.GetService(typeof(IEventSubscriberCollector)).Returns(subscriberCollector);

        var subscribers = _provider.GetSubscribers();

        Assert.That(subscribers, Is.EquivalentTo(new[] { subscriberInformation }));
    }

    #endregion
}
