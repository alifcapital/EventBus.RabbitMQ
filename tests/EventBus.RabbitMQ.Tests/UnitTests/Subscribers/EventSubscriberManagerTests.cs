using System.Reflection;
using EventBus.RabbitMQ.Configurations;
using EventBus.RabbitMQ.Subscribers.Consumers;
using EventBus.RabbitMQ.Subscribers.Managers;
using EventBus.RabbitMQ.Subscribers.Models;
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
        var queueName = "TestQueue";
        var options = new Action<EventSubscriberOptions>(x => { x.QueueName = queueName; });

        _subscriberManager.AddSubscriber<SimpleSubscribeEvent, SimpleEventSubscriberHandler>(options);

        var subscribers = GetSubscribers();
        subscribers.Should().ContainKey(nameof(SimpleSubscribeEvent));
        subscribers!.First().Value.Settings.QueueName.Should().Be(queueName);
    }
 
    [Test]
    public void AddSubscriber_RegisteringOneEventTwice_ShouldBeRegisteredOnlyOneEventWithOneSubscriber()
    {
        var typeOfEvent = typeof(SimpleSubscribeEvent);
        var typeOfHandler = typeof(SimpleEventSubscriberHandler);
        _subscriberManager.AddSubscriber<SimpleSubscribeEvent, SimpleEventSubscriberHandler>();
        _subscriberManager.AddSubscriber<SimpleSubscribeEvent, SimpleEventSubscriberHandler>();

        var subscribers = GetSubscribers();
        Assert.That(subscribers.ContainsKey(typeOfEvent.Name), Is.True);
        
        var subscribersInfo = subscribers[typeOfEvent.Name];
        Assert.That(subscribersInfo.Subscribers.Count, Is.EqualTo(1));
        
        var subscriberInfo = subscribersInfo.Subscribers.First();
        Assert.That(subscriberInfo.EventType, Is.EqualTo(typeOfEvent));
        Assert.That(subscriberInfo.EventSubscriberType, Is.EqualTo(typeOfHandler));
    }
 
    [Test]
    public void AddSubscriber_AddingExistingEventWithNewOptions_ShouldUpdateEventOptions()
    {
        var newQueueName = "TestQueueUpdated";
        var options = new Action<EventSubscriberOptions>(x => { x.QueueName = "TestQueue"; });

        _subscriberManager.AddSubscriber<SimpleSubscribeEvent, SimpleEventSubscriberHandler>(options);
        _subscriberManager.AddSubscriber<SimpleSubscribeEvent, SimpleEventSubscriberHandler>(x =>
        {
            x.QueueName = newQueueName;
        });

        var subscribers = GetSubscribers();
        subscribers.Should().ContainKey(nameof(SimpleSubscribeEvent));
        subscribers!.First().Value.Settings.QueueName.Should().Be(newQueueName);
    }

    [Test]
    public void AddSubscriber_CallingWithTypesOfEventAndTypeOfHandlerAndWithOptions_ShouldAdded()
    {
        var queueName = "TestQueue";
        var options = new EventSubscriberOptions
        {
            QueueName = "TestQueue",
        };

        _subscriberManager.AddSubscriber(typeof(SimpleSubscribeEvent), typeof(SimpleEventSubscriberHandler), options);

        var subscribers = GetSubscribers();
        subscribers.Should().ContainKey(nameof(SimpleSubscribeEvent));
        subscribers!.First().Value.Settings.QueueName.Should().Be(queueName);
    }
 
    [Test]
    public void AddSubscriber_RegisteringOneEventTwiceByType_ShouldBeRegisteredOnlyOneEventWithOneSubscriber()
    {
        var typeOfEvent = typeof(SimpleSubscribeEvent);
        var typeOfHandler = typeof(SimpleEventSubscriberHandler);
        var options = new EventSubscriberOptions();
        _subscriberManager.AddSubscriber(typeOfEvent, typeOfHandler, options);
        _subscriberManager.AddSubscriber(typeOfEvent, typeOfHandler, options);

        var subscribers = GetSubscribers();
        Assert.That(subscribers.ContainsKey(typeOfEvent.Name), Is.True);
        
        var subscribersInfo = subscribers[typeOfEvent.Name];
        Assert.That(subscribersInfo.Subscribers.Count, Is.EqualTo(1));
        
        var subscriberInfo = subscribersInfo.Subscribers.First();
        Assert.That(subscriberInfo.EventType, Is.EqualTo(typeOfEvent));
        Assert.That(subscriberInfo.EventSubscriberType, Is.EqualTo(typeOfHandler));
    }

    [Test]
    public void AddSubscriber_CallingWithTypesAddingExistingEventWithNewOptions_ShouldUpdateEventOptions()
    {
        var newQueueName = "TestQueueUpdated";
        var options = new EventSubscriberOptions
        {
            QueueName = "TestQueue",
        };

        _subscriberManager.AddSubscriber(typeof(SimpleSubscribeEvent), typeof(SimpleEventSubscriberHandler), options);
        _subscriberManager.AddSubscriber(typeof(SimpleSubscribeEvent), typeof(SimpleEventSubscriberHandler),
            new EventSubscriberOptions
            {
                QueueName = newQueueName
            });

        var subscribers = GetSubscribers();
        subscribers.Should().ContainKey(nameof(SimpleSubscribeEvent));
        subscribers!.First().Value.Settings.QueueName.Should().Be(newQueueName);
    }

    [Test]
    public void AddSubscriber_CallingWithTypesAndWithDefaultSettings_ShouldAdded()
    {
        _subscriberManager.AddSubscriber(typeof(SimpleSubscribeEvent), typeof(SimpleEventSubscriberHandler),
            new EventSubscriberOptions());
        
        var subscribers = GetSubscribers();
        subscribers.Should().ContainKey(nameof(SimpleSubscribeEvent));
        subscribers!.First().Value.Settings.QueueName.Should().BeNull();
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

        _subscriberManager.SetVirtualHostAndOwnSettingsOfSubscribers(virtualHostsSettings);

        var subscribers = GetSubscribers();
        subscribers.Should().ContainKey(nameof(SimpleSubscribeEvent));
        subscribers!.First().Value.Settings.VirtualHostSettings.VirtualHost.Should().Be("TestVirtualHost");
    }

    #endregion

    #region CreateConsumerForEachQueueAndStartReceivingEvents

    [Test]
    public void CreateConsumerForEachQueueAndStartReceivingEvents_WithSubscribers_ShouldCreateConsumer()
    {
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

        _subscriberManager.CreateConsumerForEachQueueAndStartReceivingEvents();

        var eventConsumers = GetEventConsumerServices();

        eventConsumers.Should().ContainKey("TestVirtualHost-TestQueue");
        eventConsumer.Received().AddSubscriber(
            Arg.Is<SubscribersInformation>
            (subscribersInfo =>
                subscribersInfo.EventTypeName == nameof(SimpleSubscribeEvent) &&
                subscribersInfo.Settings.QueueName == "TestQueue"
            )
        );
    }

    #endregion

    #region Helper method

    private static readonly FieldInfo SubscribersProperty = typeof(EventSubscriberManager)
        .GetField("Subscribers", BindingFlags.NonPublic | BindingFlags.Static);
    
    Dictionary<string, SubscribersInformation> GetSubscribers()
    {
        var eventSubscriberManager = typeof(EventSubscriberManager);
        var subscribers =
            (Dictionary<string, SubscribersInformation>)
            SubscribersProperty?.GetValue(eventSubscriberManager)!;

        return subscribers;
    }

    private Dictionary<string, IEventConsumerService> GetEventConsumerServices()
    {
        const string eventConsumersFieldName = "_eventConsumers";
        var field = _subscriberManager.GetType()
            .GetField(eventConsumersFieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        field.Should().NotBeNull();
        var eventConsumers = (Dictionary<string, IEventConsumerService>)field?.GetValue(_subscriberManager)!;
        return eventConsumers;
    }

    #endregion
}