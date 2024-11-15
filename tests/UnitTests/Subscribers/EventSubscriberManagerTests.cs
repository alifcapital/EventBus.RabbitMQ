using System.Reflection;
using EventBus.RabbitMQ.Configurations;
using EventBus.RabbitMQ.Subscribers.Consumers;
using EventBus.RabbitMQ.Subscribers.Managers;
using EventBus.RabbitMQ.Subscribers.Options;
using EventBus.RabbitMQ.Tests.Domain;
using FluentAssertions;
using NSubstitute;

namespace EventBus.RabbitMQ.Tests.UnitTests.Subscribers;

public class EventSubscriberManagerTests : BaseTestEntity
{
    private IServiceProvider _serviceProvider;
    private EventSubscriberManager _subscriberManager;

    #region SutUp

    [SetUp]
    public void Setup()
    {
        var settings = RabbitMqOptionsConstant.CreateDefaultRabbitMqOptions();
        _serviceProvider = Substitute.For<IServiceProvider>();
        _subscriberManager = new EventSubscriberManager(settings, _serviceProvider);
    }

    #endregion

    #region AddSubscriber

    [Test]
    public void AddSubscriber_CallingWithGenericAndWithOption_ShouldAdded()
    {
        var options = new Action<EventSubscriberOptions>(x => { x.QueueName = "TestQueue"; });

        // Act
        _subscriberManager.AddSubscriber<SimpleSubscribeEvent, SimpleEventSubscriberHandler>(options);

        // Assert
        var field = _subscriberManager.GetType()
            .GetField("_subscribers", BindingFlags.NonPublic | BindingFlags.Instance);
        field.Should().NotBeNull();
        var subscribers =
            (Dictionary<string, (Type eventType, Type eventHandlerType, EventSubscriberOptions eventSettings)>)
            field?.GetValue(_subscriberManager)!;

        subscribers.Should().ContainKey(nameof(SimpleSubscribeEvent));
        subscribers!.First().Value.eventSettings.QueueName.Should().Be("TestQueue");
    }

    [Test]
    public void AddSubscriber_AddingExistingEventWithNewOptions_ShouldUpdateEventOptions()
    {
        var options = new Action<EventSubscriberOptions>(x => { x.QueueName = "TestQueue"; });

        // Act
        _subscriberManager.AddSubscriber<SimpleSubscribeEvent, SimpleEventSubscriberHandler>(options);
        _subscriberManager.AddSubscriber<SimpleSubscribeEvent, SimpleEventSubscriberHandler>(x =>
        {
            x.QueueName = "TestQueueUpdated";
        });

        // Assert
        var field = _subscriberManager.GetType()
            .GetField("_subscribers", BindingFlags.NonPublic | BindingFlags.Instance);
        field.Should().NotBeNull();
        var subscribers =
            (Dictionary<string, (Type eventType, Type eventHandlerType, EventSubscriberOptions eventSettings)>)
            field?.GetValue(_subscriberManager)!;

        subscribers.Should().ContainKey(nameof(SimpleSubscribeEvent));
        subscribers!.First().Value.eventSettings.QueueName.Should().Be("TestQueueUpdated");
    }

    [Test]
    public void AddSubscriber_CallingWithTypesOfEventAndTypeOfHandlerAndWithOptions_ShouldAdded()
    {
        var options = new EventSubscriberOptions
        {
            QueueName = "TestQueue",
        };

        // Act
        _subscriberManager.AddSubscriber(typeof(SimpleSubscribeEvent), typeof(SimpleEventSubscriberHandler), options);

        // Assert
        var field = _subscriberManager.GetType()
            .GetField("_subscribers", BindingFlags.NonPublic | BindingFlags.Instance);
        field.Should().NotBeNull();
        var subscribers =
            (Dictionary<string, (Type eventType, Type eventHandlerType, EventSubscriberOptions eventSettings)>)
            field?.GetValue(_subscriberManager)!;

        subscribers.Should().ContainKey(nameof(SimpleSubscribeEvent));
        subscribers!.First().Value.eventSettings.QueueName.Should().Be("TestQueue");
    }

    [Test]
    public void AddSubscriber_CallingWithTypesAddingExistingEventWithNewOptions_ShouldUpdateEventOptions()
    {
        var options = new EventSubscriberOptions
        {
            QueueName = "TestQueue",
        };

        // Act
        _subscriberManager.AddSubscriber(typeof(SimpleSubscribeEvent), typeof(SimpleEventSubscriberHandler), options);
        _subscriberManager.AddSubscriber(typeof(SimpleSubscribeEvent), typeof(SimpleEventSubscriberHandler),
            new EventSubscriberOptions
            {
                QueueName = "TestQueueUpdated"
            });

        // Assert
        var field = _subscriberManager.GetType()
            .GetField("_subscribers", BindingFlags.NonPublic | BindingFlags.Instance);
        field.Should().NotBeNull();
        var subscribers =
            (Dictionary<string, (Type eventType, Type eventHandlerType, EventSubscriberOptions eventSettings)>)
            field?.GetValue(_subscriberManager)!;

        subscribers.Should().ContainKey(nameof(SimpleSubscribeEvent));
        subscribers!.First().Value.eventSettings.QueueName.Should().Be("TestQueueUpdated");
    }

    [Test]
    public void AddSubscriber_CallingWithTypesAndWithDefaultSettings_ShouldAdded()
    {
        // Act
        _subscriberManager.AddSubscriber(typeof(SimpleSubscribeEvent), typeof(SimpleEventSubscriberHandler),
            new EventSubscriberOptions());

        // Assert
        var field = _subscriberManager.GetType()
            .GetField("_subscribers", BindingFlags.NonPublic | BindingFlags.Instance);
        field.Should().NotBeNull();
        var subscribers =
            (Dictionary<string, (Type eventType, Type eventHandlerType, EventSubscriberOptions eventSettings)>)
            field?.GetValue(_subscriberManager)!;

        subscribers.Should().ContainKey(nameof(SimpleSubscribeEvent));
        subscribers!.First().Value.eventSettings.QueueName.Should().BeNull();
    }

    #endregion

    #region SetVirtualHostAndOwnSettingsOfSubscribers

    [Test]
    public void SetVirtualHostAndOwnSettingsOfSubscribers_WithVirtualHostSettings_ShouldSetVirtualHostAndOwnSettings()
    {
        var virtualHostsSettings = new Dictionary<string, RabbitMqHostSettings>
        {
            {
                "TestVirtualHostKey", new RabbitMqHostSettings
                {
                    VirtualHost = "TestVirtualHost",
                    ExchangeName = "TestExchangeName"
                }
            }
        };

        var options = new Action<EventSubscriberOptions>(x => { x.VirtualHostKey = "TestVirtualHostKey"; });

        _subscriberManager.AddSubscriber<SimpleSubscribeEvent, SimpleEventSubscriberHandler>(options);

        // Act
        _subscriberManager.SetVirtualHostAndOwnSettingsOfSubscribers(virtualHostsSettings);

        // Assert
        var field = _subscriberManager.GetType()
            .GetField("_subscribers", BindingFlags.NonPublic | BindingFlags.Instance);
        field.Should().NotBeNull();
        var subscribers =
            (Dictionary<string, (Type eventType, Type eventHandlerType, EventSubscriberOptions eventSettings)>)
            field?.GetValue(_subscriberManager)!;

        subscribers.Should().ContainKey(nameof(SimpleSubscribeEvent));
        subscribers!.First().Value.eventSettings.VirtualHostSettings.VirtualHost.Should().Be("TestVirtualHost");
    }

    #endregion

    #region CreateConsumerForEachQueueAndStartReceivingEvents

    [Test]
    public void CreateConsumerForEachQueueAndStartReceivingEvents_WithSubscribers_ShouldCreateConsumer()
    {
        // Arrange
        var options = new Action<EventSubscriberOptions>(x =>
        {
            x.VirtualHostKey = "TestVirtualHostKey";
            x.QueueName = "TestQueue";
        });

        var virtualHostsSettings = new Dictionary<string, RabbitMqHostSettings>
        {
            {
                "TestVirtualHostKey", new RabbitMqHostSettings
                {
                    VirtualHost = "TestVirtualHost",
                    ExchangeName = "TestExchangeName"
                }
            }
        };

        var eventConsumerServiceCreator = Substitute.For<IEventConsumerServiceCreator>();
        _serviceProvider.GetService(typeof(IEventConsumerServiceCreator)).Returns(eventConsumerServiceCreator);

        var eventConsumer = Substitute.For<IEventConsumerService>();
        eventConsumerServiceCreator.Create(
                Arg.Any<EventSubscriberOptions>(),
                _serviceProvider,
                false
            )
            .Returns(eventConsumer);

        _subscriberManager.AddSubscriber<SimpleSubscribeEvent, SimpleEventSubscriberHandler>(options);
        _subscriberManager.SetVirtualHostAndOwnSettingsOfSubscribers(virtualHostsSettings);

        // Act
        _subscriberManager.CreateConsumerForEachQueueAndStartReceivingEvents();

        // Assert
        var field = _subscriberManager.GetType()
            .GetField("_eventConsumers", BindingFlags.NonPublic | BindingFlags.Instance);
        field.Should().NotBeNull();
        var eventConsumers = (Dictionary<string, IEventConsumerService>)field?.GetValue(_subscriberManager)!;

        eventConsumers.Should().ContainKey("TestVirtualHost-TestQueue");
        eventConsumer.Received().AddSubscriber(
            Arg.Is<(Type eventType, Type eventHandlerType, EventSubscriberOptions eventSettings)>
            (evInfo =>
                evInfo.eventHandlerType == typeof(SimpleEventSubscriberHandler) &&
                evInfo.eventType == typeof(SimpleSubscribeEvent) &&
                evInfo.eventSettings.QueueName == "TestQueue"
            )
        );
    }

    #endregion
}