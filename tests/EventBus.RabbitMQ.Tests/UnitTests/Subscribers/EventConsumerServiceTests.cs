using System.Reflection;
using EventBus.RabbitMQ.Configurations;
using EventBus.RabbitMQ.Connections;
using EventBus.RabbitMQ.Subscribers.Consumers;
using EventBus.RabbitMQ.Subscribers.Models;
using EventBus.RabbitMQ.Subscribers.Options;
using EventBus.RabbitMQ.Tests.Domain;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RabbitMQ.Client;

namespace EventBus.RabbitMQ.Tests.UnitTests.Subscribers;

public class EventConsumerServiceTests : BaseTestEntity
{
    private IServiceProvider _serviceProvider;
    private EventConsumerService _consumerService;
    private ILogger<EventConsumerService> _logger;
    private IRabbitMqConnectionManager _rabbitMqConnectionManager;
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

    #region StartAndSubscribeReceiver

    [Test]
    public void StartAndSubscribeReceiver_StartingConsumerWithDefaultSetting_ShouldCreateConsumer()
    {
        _consumerService.CreateChannelAndSubscribeReceiver();

        var field = _consumerService.GetType()
            .GetField("_consumerChannel", BindingFlags.NonPublic | BindingFlags.Instance);
        
        Assert.That(field, Is.Not.Null);
        var consumerChannel = field?.GetValue(_consumerService) as IModel;
        _rabbitMqConnectionManager.Received(1).GetOrCreateConnection(_settings.VirtualHostSettings);
        Assert.That(consumerChannel, Is.Not.Null);
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

    #endregion
}