using System.Reflection;
using EventBus.RabbitMQ.Configurations;
using EventBus.RabbitMQ.Connections;
using EventBus.RabbitMQ.Subscribers.Consumers;
using EventBus.RabbitMQ.Subscribers.Options;
using EventBus.RabbitMQ.Tests.Domain;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RabbitMQ.Client;

namespace EventBus.RabbitMQ.Tests.UnitTests.Subscribers;

public class EventConsumerServiceTests : BaseTestEntity
{
    private IServiceProvider _serviceProvider;
    private EventConsumerService _consumerService;
    private ILogger<EventConsumerService> _logger;
    private IRabbitMqConnectionCreator _rabbitMqConnectionCreator;
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

        _rabbitMqConnectionCreator = Substitute.For<IRabbitMqConnectionCreator>();
        _serviceProvider.GetService(typeof(IRabbitMqConnectionCreator)).Returns(_rabbitMqConnectionCreator);

        _consumerService = new EventConsumerService(_settings, _serviceProvider, false);
    }

    #endregion

    #region AddSubscriber

    [Test]
    public void AddSubscriber_WithOptionsQueueName_ShouldAddSubscriber()
    {
        var queueName = "test-queue";
        var settings = new EventSubscriberOptions
        {
            EventTypeName = nameof(SimpleSubscribeEvent),
            QueueName = queueName,
        };
        var eventInfo = (typeof(SimpleSubscribeEvent), typeof(SimpleEventSubscriberHandler), settings);

        _consumerService.AddSubscriber(eventInfo);

        var field = _consumerService.GetType()
            .GetField("_subscribers", BindingFlags.NonPublic | BindingFlags.Instance);
        field.Should().NotBeNull();
        
        var subscribers =
            (Dictionary<string, (Type eventType, Type eventHandlerType, EventSubscriberOptions eventSettings)>)
            field?.GetValue(_consumerService)!;
        subscribers.Should().ContainKey(nameof(SimpleSubscribeEvent));
        subscribers?.First().Value.eventSettings.QueueName.Should().Be(queueName);
    }

    #endregion

    #region StartAndSubscribeReceiver

    [Test]
    public void StartAndSubscribeReceiver_StartingConsumerWithDefaultSetting_ShouldCreateConsumer()
    {
        _consumerService.StartAndSubscribeReceiver();

        var field = _consumerService.GetType()
            .GetField("_consumerChannel", BindingFlags.NonPublic | BindingFlags.Instance);
        field.Should().NotBeNull();
        var consumerChannel = (IModel)field?.GetValue(_consumerService)!;
        _rabbitMqConnectionCreator.Received(1).CreateConnection(_settings, _serviceProvider);

        consumerChannel.Should().NotBeNull();
    }

    #endregion
}