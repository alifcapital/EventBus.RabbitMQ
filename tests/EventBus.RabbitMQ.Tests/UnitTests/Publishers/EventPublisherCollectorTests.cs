using System.Reflection;
using EventBus.RabbitMQ.Configurations;
using EventBus.RabbitMQ.Connections;
using EventBus.RabbitMQ.Publishers.Managers;
using EventBus.RabbitMQ.Publishers.Options;
using EventBus.RabbitMQ.Tests.Domain;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RabbitMQ.Client;

namespace EventBus.RabbitMQ.Tests.UnitTests.Publishers;

public class EventPublisherCollectorTests : BaseTestEntity
{
    private IServiceProvider _serviceProvider;
    private EventPublisherCollector _publisherCollector;
    private IRabbitMqConnectionManager _rabbitMqConnectionManager;

    #region SetUp

    [SetUp]
    public void Setup()
    {
        _serviceProvider = Substitute.For<IServiceProvider>();
        _serviceProvider.GetService(typeof(RabbitMqOptions))
            .Returns(RabbitMqOptionsConstant.CreateDefaultRabbitMqOptions());
        _serviceProvider.GetService(typeof(ILogger<EventPublisherCollector>))
            .Returns(Substitute.For<ILogger<EventPublisherCollector>>());
        _rabbitMqConnectionManager = Substitute.For<IRabbitMqConnectionManager>();
        _serviceProvider.GetService(typeof(IRabbitMqConnectionManager)).Returns(_rabbitMqConnectionManager);
        _publisherCollector = new EventPublisherCollector(_serviceProvider);
    }

    #endregion

    #region AddPublisher

    [Test]
    public void AddPublisher_CallingWithGenericAndWithOption_ShouldAdded()
    {
        // Arrange
        var options = new Action<EventPublisherOptions>(x => { x.RoutingKey = "TestRoutingKey"; });

        // Act
        _publisherCollector.AddPublisher<SimplePublishEvent>(options);

        // Assert
        var publishers = GetPublishersInfo();
        Assert.That(publishers.ContainsKey(nameof(SimplePublishEvent)), Is.True);
        Assert.That(publishers[nameof(SimplePublishEvent)].RoutingKey, Is.EqualTo("TestRoutingKey"));
    }

    [Test]
    public void AddPublisher_AddingExistingEventWithNewOptions_ShouldUpdateEventOptions()
    {
        // Arrange
        var options = new Action<EventPublisherOptions>(x => { x.RoutingKey = "TestRoutingKey"; });

        // Act
        _publisherCollector.AddPublisher<SimplePublishEvent>(options);
        _publisherCollector.AddPublisher<SimplePublishEvent>(x => { x.RoutingKey = "TestRoutingKeyUpdated"; });

        // Assert
        var publishers = GetPublishersInfo();
        Assert.That(publishers.ContainsKey(nameof(SimplePublishEvent)), Is.True);
        Assert.That(publishers[nameof(SimplePublishEvent)].RoutingKey, Is.EqualTo("TestRoutingKeyUpdated"));
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
        _publisherCollector.AddPublisher(typeOfPublisher, publisherSettings);

        // Assert
        var publishers = GetPublishersInfo();
        Assert.That(publishers.ContainsKey(nameof(SimplePublishEvent)), Is.True);
        Assert.That(publishers[nameof(SimplePublishEvent)].RoutingKey, Is.EqualTo("TestRoutingKey"));
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
        _publisherCollector.AddPublisher<SimplePublishEvent>(publisherSettings);

        // Act
        _publisherCollector.SetVirtualHostAndOwnSettingsOfPublishers(virtualHostsSettings);

        // Assert
        var publishers = GetPublishersInfo();
        Assert.That(publishers.ContainsKey(nameof(SimplePublishEvent)), Is.True);
        Assert.That(publishers[nameof(SimplePublishEvent)].VirtualHostSettings.VirtualHost, Is.EqualTo("TestVirtualHost"));
    }

    #endregion

    #region CreateExchangeForPublishers

    [Test]
    public async Task CreateExchangeForPublishers_CallingWithPublisherSettings_ShouldCreateExchange()
    {
        var options = new Action<EventPublisherOptions>(x => { x.RoutingKey = "TestRoutingKey"; });
        _publisherCollector.AddPublisher<SimplePublishEvent>(options);
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
        _publisherCollector.SetVirtualHostAndOwnSettingsOfPublishers(virtualHostsSettings);
        var rabbitMqConnection = Substitute.For<IRabbitMqConnection>();
        var channel = Substitute.For<IChannel>();
        rabbitMqConnection.CreateChannel().Returns(Task.FromResult(channel));
        channel.ExchangeDeclareAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<IDictionary<string, object>>(),
                Arg.Any<bool>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _rabbitMqConnectionManager.GetOrCreateConnection(Arg.Any<RabbitMqHostSettings>())
            .Returns(rabbitMqConnection);

        await _publisherCollector.CreateExchangeForPublishers();

        _rabbitMqConnectionManager.Received(1).GetOrCreateConnection(Arg.Any<RabbitMqHostSettings>());
        await rabbitMqConnection.Received(1).CreateChannel();
    }

    #endregion
    
    #region Helper methods
    
    private static readonly FieldInfo PublishersField = typeof(EventPublisherCollector)
        .GetField("_publishersConnectionInfo", BindingFlags.NonPublic | BindingFlags.Instance);
    
    Dictionary<string, EventPublisherOptions> GetPublishersInfo()
    {
        var publishers = (Dictionary<string, EventPublisherOptions>)PublishersField?.GetValue(_publisherCollector)!;
        return publishers;
    }

    #endregion
}
