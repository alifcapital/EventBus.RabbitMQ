using System.Reflection;
using EventBus.RabbitMQ.Configurations;
using EventBus.RabbitMQ.Connections;
using EventBus.RabbitMQ.Publishers.Managers;
using EventBus.RabbitMQ.Publishers.Options;
using EventBus.RabbitMQ.Tests.Domain;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace EventBus.RabbitMQ.Tests.UnitTests.Publishers;

public class EventPublisherManagerTests : BaseTestEntity
{
    private IServiceProvider _serviceProvider;
    private EventPublisherManager _publisherManager;
    private IRabbitMqConnectionCreator _rabbitMqConnectionCreator;

    #region SetUp

    [SetUp]
    public void Setup()
    {
        _serviceProvider = Substitute.For<IServiceProvider>();
        _serviceProvider.GetService(typeof(RabbitMqOptions))
            .Returns(RabbitMqOptionsConstant.CreateDefaultRabbitMqOptions());
        _serviceProvider.GetService(typeof(ILogger<EventPublisherManager>))
            .Returns(Substitute.For<ILogger<EventPublisherManager>>());
        _rabbitMqConnectionCreator = Substitute.For<IRabbitMqConnectionCreator>();
        _serviceProvider.GetService(typeof(IRabbitMqConnectionCreator)).Returns(_rabbitMqConnectionCreator);
        _publisherManager = new EventPublisherManager(_serviceProvider);
    }

    #endregion

    #region AddPublisher

    [Test]
    public void AddPublisher_CallingWithGenericAndWithOption_ShouldAdded()
    {
        // Arrange
        var options = new Action<EventPublisherOptions>(x => { x.RoutingKey = "TestRoutingKey"; });

        // Act
        _publisherManager.AddPublisher<SimplePublishEvent>(options);

        // Assert
        var publishers = GetPublishersInfo();
        publishers.Should().ContainKey(nameof(SimplePublishEvent));
        publishers!.First().Value.RoutingKey.Should().Be("TestRoutingKey");
    }

    [Test]
    public void AddPublisher_AddingExistingEventWithNewOptions_ShouldUpdateEventOptions()
    {
        // Arrange
        var options = new Action<EventPublisherOptions>(x => { x.RoutingKey = "TestRoutingKey"; });

        // Act
        _publisherManager.AddPublisher<SimplePublishEvent>(options);
        _publisherManager.AddPublisher<SimplePublishEvent>(x => { x.RoutingKey = "TestRoutingKeyUpdated"; });

        // Assert
        var publishers = GetPublishersInfo();
        publishers.Should().ContainKey(nameof(SimplePublishEvent));
        publishers!.First().Value.RoutingKey.Should().Be("TestRoutingKeyUpdated");
    }

    [Test]
    public void AddPublisher_CallingWithTypeAndOptions_ShouldAdded()
    {
        // Arrange
        var typeOfPublisher = typeof(SimplePublishEvent);
        var publisherSettings = new EventPublisherOptions
        {
            RoutingKey = "TestRoutingKey"
        };

        // Act
        _publisherManager.AddPublisher(typeOfPublisher, publisherSettings);

        // Assert
        var publishers = GetPublishersInfo();
        publishers.Should().ContainKey(nameof(SimplePublishEvent));
        publishers!.First().Value.RoutingKey.Should().Be("TestRoutingKey");
    }

    #endregion

    #region SetVirtualHostAndOwnSettingsOfPublishers

    [Test]
    public void SetVirtualHostAndOwnSettingsOfPublishers_SettingVirtualHostAndOwnSettings_ShouldSet()
    {
        // Arrange
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

        var publisherSettings = new Action<EventPublisherOptions>(x => { x.VirtualHostKey = "TestVirtualHostKey"; });
        _publisherManager.AddPublisher<SimplePublishEvent>(publisherSettings);

        // Act
        _publisherManager.SetVirtualHostAndOwnSettingsOfPublishers(virtualHostsSettings);

        // Assert
        var publishers = GetPublishersInfo();
        publishers.Should().ContainKey(nameof(SimplePublishEvent));
        publishers!.First().Value.VirtualHostSettings.VirtualHost.Should().Be("TestVirtualHost");
    }

    #endregion

    #region CreateExchangeForPublishers

    [Test]
    public void CreateExchangeForPublishers_CallingWithPublisherSettings_ShouldCreateExchange()
    {
        // Arrange
        var options = new Action<EventPublisherOptions>(x => { x.RoutingKey = "TestRoutingKey"; });
        _publisherManager.AddPublisher<SimplePublishEvent>(options);
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
        _publisherManager.SetVirtualHostAndOwnSettingsOfPublishers(virtualHostsSettings);
        var rabbitMqConnection = Substitute.For<IRabbitMqConnection>();
        _rabbitMqConnectionCreator.CreateConnection(Arg.Any<EventPublisherOptions>(), _serviceProvider)
            .Returns(rabbitMqConnection);

        // Act
        _publisherManager.CreateExchangeForPublishers();

        // Assert
        var field = _publisherManager.GetType()
            .GetField("_openedRabbitMqConnections", BindingFlags.NonPublic | BindingFlags.Instance);
        field.Should().NotBeNull();
        var openedRabbitMqConnections = (Dictionary<string, IRabbitMqConnection>)field?.GetValue(_publisherManager)!;
        _rabbitMqConnectionCreator.Received(1).CreateConnection(Arg.Any<EventPublisherOptions>(), _serviceProvider);
        rabbitMqConnection.Received(1).CreateChannel();

        openedRabbitMqConnections?.Count.Should().Be(1);
    }

    #endregion

    #region Publish

    [Test]
    public async Task Publish_PublishingWithOptionsWithRoutingKey_ShouldCreatedConnection()
    {
        // Arrange
        var options = new Action<EventPublisherOptions>(x => { x.RoutingKey = "TestRoutingKey"; });
        _publisherManager.AddPublisher<SimplePublishEvent>(options);
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
        _publisherManager.SetVirtualHostAndOwnSettingsOfPublishers(virtualHostsSettings);
        _serviceProvider.GetService(typeof(ILogger<RabbitMqConnection>))
            .Returns(Substitute.For<ILogger<RabbitMqConnection>>());

        _publisherManager.CreateExchangeForPublishers();
        var @event = new SimplePublishEvent
        {
            Id = Guid.NewGuid(),
            Name = "TestName"
        };

        // Act
        await _publisherManager.PublishAsync(@event);

        // Assert
        var field = _publisherManager.GetType()
            .GetField("_openedRabbitMqConnections", BindingFlags.NonPublic | BindingFlags.Instance);
        field.Should().NotBeNull();
        var openedRabbitMqConnections = (Dictionary<string, IRabbitMqConnection>)field?.GetValue(_publisherManager)!;

        openedRabbitMqConnections?.Count.Should().Be(1);
    }

    #endregion
    
    #region Helper methods
    
    private static readonly FieldInfo PublishersField = typeof(EventPublisherManager)
        .GetField("_publishersConnectionInfo", BindingFlags.NonPublic | BindingFlags.Instance);
    
    Dictionary<string, EventPublisherOptions> GetPublishersInfo()
    {
        var publishers = (Dictionary<string, EventPublisherOptions>)PublishersField?.GetValue(_publisherManager)!;
        return publishers;
    }
    
    #endregion
}