using System.Reflection;
using EventBus.RabbitMQ.Configurations;
using EventBus.RabbitMQ.Subscribers.Consumers;
using EventBus.RabbitMQ.Subscribers.Managers;
using EventBus.RabbitMQ.Subscribers.Models;
using EventBus.RabbitMQ.Subscribers.Options;
using EventBus.RabbitMQ.Tests.Domain;
using NSubstitute;

namespace EventBus.RabbitMQ.Tests.UnitTests.Subscribers;

public class EventSubscriberCollectorTests : BaseTestEntity
{
    private IServiceProvider _serviceProvider;
    private EventSubscriberCollector _subscriberCollector;

    #region SutUp

    [SetUp]
    public void Setup()
    {
        var settings = RabbitMqOptionsConstant.CreateDefaultRabbitMqOptions();
        _serviceProvider = Substitute.For<IServiceProvider>();
        _subscriberCollector = new EventSubscriberCollector(settings, _serviceProvider);
    }

    #endregion

    #region AddSubscriber

    [Test]
    public void AddSubscriber_CallingWithGenericAndWithOption_ShouldAdded()
    {
        var queueName = "TestQueue";
        var options = new Action<EventSubscriberOptions>(x => { x.QueueName = queueName; });

        _subscriberCollector.AddSubscriber<SimpleSubscribeEvent, SimpleEventSubscriberHandler>(options);

        var subscribers = GetSubscribers();
        Assert.That(subscribers.ContainsKey(nameof(SimpleSubscribeEvent)), Is.True);
        Assert.That(subscribers[nameof(SimpleSubscribeEvent)].Settings.QueueName, Is.EqualTo(queueName));
    }
 
    [Test]
    public void AddSubscriber_RegisteringOneEventTwice_ShouldBeRegisteredOnlyOneEventWithOneSubscriber()
    {
        var typeOfEvent = typeof(SimpleSubscribeEvent);
        var typeOfHandler = typeof(SimpleEventSubscriberHandler);
        _subscriberCollector.AddSubscriber<SimpleSubscribeEvent, SimpleEventSubscriberHandler>();
        _subscriberCollector.AddSubscriber<SimpleSubscribeEvent, SimpleEventSubscriberHandler>();

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

        _subscriberCollector.AddSubscriber<SimpleSubscribeEvent, SimpleEventSubscriberHandler>(options);
        _subscriberCollector.AddSubscriber<SimpleSubscribeEvent, SimpleEventSubscriberHandler>(x =>
        {
            x.QueueName = newQueueName;
        });

        var subscribers = GetSubscribers();
        Assert.That(subscribers.ContainsKey(nameof(SimpleSubscribeEvent)), Is.True);
        Assert.That(subscribers[nameof(SimpleSubscribeEvent)].Settings.QueueName, Is.EqualTo(newQueueName));
    }

    [Test]
    public void AddSubscriber_CallingWithTypesOfEventAndTypeOfHandlerAndWithOptions_ShouldAdded()
    {
        var queueName = "TestQueue";
        var options = new EventSubscriberOptions
        {
            QueueName = "TestQueue",
        };

        _subscriberCollector.AddSubscriber(typeof(SimpleSubscribeEvent), typeof(SimpleEventSubscriberHandler), options);

        var subscribers = GetSubscribers();
        Assert.That(subscribers.ContainsKey(nameof(SimpleSubscribeEvent)), Is.True);
        Assert.That(subscribers[nameof(SimpleSubscribeEvent)].Settings.QueueName, Is.EqualTo(queueName));
    }
 
    [Test]
    public void AddSubscriber_RegisteringOneEventTwiceByType_ShouldBeRegisteredOnlyOneEventWithOneSubscriber()
    {
        var typeOfEvent = typeof(SimpleSubscribeEvent);
        var typeOfHandler = typeof(SimpleEventSubscriberHandler);
        var options = new EventSubscriberOptions();
        _subscriberCollector.AddSubscriber(typeOfEvent, typeOfHandler, options);
        _subscriberCollector.AddSubscriber(typeOfEvent, typeOfHandler, options);

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

        _subscriberCollector.AddSubscriber(typeof(SimpleSubscribeEvent), typeof(SimpleEventSubscriberHandler), options);
        _subscriberCollector.AddSubscriber(typeof(SimpleSubscribeEvent), typeof(SimpleEventSubscriberHandler),
            new EventSubscriberOptions
            {
                QueueName = newQueueName
            });

        var subscribers = GetSubscribers();
        Assert.That(subscribers.ContainsKey(nameof(SimpleSubscribeEvent)), Is.True);
        Assert.That(subscribers[nameof(SimpleSubscribeEvent)].Settings.QueueName, Is.EqualTo(newQueueName));
    }

    [Test]
    public void AddSubscriber_CallingWithTypesAndWithDefaultSettings_ShouldAdded()
    {
        _subscriberCollector.AddSubscriber(typeof(SimpleSubscribeEvent), typeof(SimpleEventSubscriberHandler),
            new EventSubscriberOptions());
        
        var subscribers = GetSubscribers();
        Assert.That(subscribers.ContainsKey(nameof(SimpleSubscribeEvent)), Is.True);
        Assert.That(subscribers[nameof(SimpleSubscribeEvent)].Settings.QueueName, Is.Null);
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
        _subscriberCollector.AddSubscriber<SimpleSubscribeEvent, SimpleEventSubscriberHandler>(options);

        _subscriberCollector.SetVirtualHostAndOwnSettingsOfSubscribers(virtualHostsSettings);

        var subscribers = GetSubscribers();
        Assert.That(subscribers.ContainsKey(nameof(SimpleSubscribeEvent)), Is.True);
        Assert.That(subscribers[nameof(SimpleSubscribeEvent)].Settings.VirtualHostSettings.VirtualHost,
            Is.EqualTo("TestVirtualHost"));
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

        _subscriberCollector.AddSubscriber<SimpleSubscribeEvent, SimpleEventSubscriberHandler>(options);
        _subscriberCollector.SetVirtualHostAndOwnSettingsOfSubscribers(virtualHostsSettings);

        _subscriberCollector.CreateConsumerForEachQueueAndStartReceivingEvents();

        var eventConsumers = GetEventConsumerServices();

        Assert.That(eventConsumers.ContainsKey("TestVirtualHost-TestQueue"), Is.True);
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

    private static readonly FieldInfo SubscribersProperty = typeof(EventSubscriberCollector)
        .GetField("Subscribers", BindingFlags.NonPublic | BindingFlags.Static);
    
    Dictionary<string, SubscribersInformation> GetSubscribers()
    {
        var eventSubscriberManager = typeof(EventSubscriberCollector);
        var subscribers =
            (Dictionary<string, SubscribersInformation>)
            SubscribersProperty?.GetValue(eventSubscriberManager)!;

        return subscribers;
    }

    private readonly FieldInfo _eventConsumersField = typeof(EventSubscriberCollector)
        .GetField("_eventConsumers", BindingFlags.NonPublic | BindingFlags.Instance);

    private Dictionary<string, IEventConsumerService> GetEventConsumerServices()
    {
        Assert.That(_eventConsumersField, Is.Not.Null);
        var eventConsumers =
            (Dictionary<string, IEventConsumerService>)_eventConsumersField?.GetValue(_subscriberCollector)!;
        return eventConsumers;
    }

    #endregion
}