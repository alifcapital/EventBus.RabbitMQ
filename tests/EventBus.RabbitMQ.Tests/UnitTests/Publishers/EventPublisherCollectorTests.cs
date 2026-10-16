using System.Reflection;
using EventBus.RabbitMQ.Configurations;
using EventBus.RabbitMQ.Connections;
using EventBus.RabbitMQ.Information.Models;
using EventBus.RabbitMQ.Publishers.Managers;
using EventBus.RabbitMQ.Publishers.Messaging;
using EventBus.RabbitMQ.Publishers.Models;
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
    private ILogger<EventPublisherCollector> _logger;

    #region SetUp

    [SetUp]
    public void Setup()
    {
        _serviceProvider = Substitute.For<IServiceProvider>();
        _serviceProvider.GetService(typeof(RabbitMqOptions))
            .Returns(RabbitMqOptionsConstant.CreateDefaultRabbitMqOptions());
        _logger = Substitute.For<ILogger<EventPublisherCollector>>();
        _serviceProvider.GetService(typeof(ILogger<EventPublisherCollector>))
            .Returns(_logger);
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
        Assert.That(publishers[nameof(SimplePublishEvent)].Settings.RoutingKey, Is.EqualTo("TestRoutingKey"));
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
        Assert.That(publishers[nameof(SimplePublishEvent)].Settings.RoutingKey, Is.EqualTo("TestRoutingKeyUpdated"));
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
        Assert.That(publishers[nameof(SimplePublishEvent)].Settings.RoutingKey, Is.EqualTo("TestRoutingKey"));
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
        Assert.That(publishers[nameof(SimplePublishEvent)].Settings.VirtualHostSettings.VirtualHost, Is.EqualTo("TestVirtualHost"));
    }

    #endregion

    #region CreateExchangeForPublishersAsync

    [Test]
    public async Task CreateExchangeForPublishersAsync_CallingWithPublisherSettings_ShouldCreateExchange()
    {
        var cancellationToken = CancellationToken.None;
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
        rabbitMqConnection.CreatePublisherChannelAsync(Arg.Any<bool>(), cancellationToken).Returns(Task.FromResult(channel));
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

        await _publisherCollector.CreateExchangeForPublishersAsync(cancellationToken);

        _rabbitMqConnectionManager.Received(1).GetOrCreateConnection(Arg.Any<RabbitMqHostSettings>());
        await rabbitMqConnection.Received(1).CreatePublisherChannelAsync(publisherConfirmation: true, cancellationToken);
    }

    [Test]
    public async Task CreateExchangeForPublishersAsync_AfterDeclaringExchange_ShouldDisposeChannel()
    {
        var channel = Substitute.For<IChannel>();
        SetupPublisherChannel(Task.FromResult(channel));
        GetPublishersInfo().Add("FirstEvent", CreatePublisherInformation("FirstExchange"));

        await _publisherCollector.CreateExchangeForPublishersAsync(CancellationToken.None);

        await channel.Received(1).DisposeAsync();
    }

    [Test]
    public async Task CreateExchangeForPublishersAsync_WhenFailsWhileStopping_ShouldNotLogErrorAndSkipOtherPublishers()
    {
        var rabbitMqConnection =
            SetupPublisherChannel(Task.FromException<IChannel>(new ObjectDisposedException(nameof(IServiceProvider))));
        var publishers = GetPublishersInfo();
        publishers.Add("FirstEvent", CreatePublisherInformation("FirstExchange"));
        publishers.Add("SecondEvent", CreatePublisherInformation("SecondExchange"));

        await _publisherCollector.CreateExchangeForPublishersAsync(new CancellationToken(canceled: true));

        Assert.That(HasErrorLog(), Is.False);
        await rabbitMqConnection.Received(1)
            .CreatePublisherChannelAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task CreateExchangeForPublishersAsync_WhenFailsWithoutStopping_ShouldLogErrorAndContinueWithOtherPublishers()
    {
        var rabbitMqConnection =
            SetupPublisherChannel(Task.FromException<IChannel>(new InvalidOperationException("Broker is unreachable")));
        var publishers = GetPublishersInfo();
        publishers.Add("FirstEvent", CreatePublisherInformation("FirstExchange"));
        publishers.Add("SecondEvent", CreatePublisherInformation("SecondExchange"));

        await _publisherCollector.CreateExchangeForPublishersAsync(CancellationToken.None);

        Assert.That(HasErrorLog(), Is.True);
        await rabbitMqConnection.Received(2)
            .CreatePublisherChannelAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    #endregion

    #region CreateRabbitMqChannelAsync

    [Test]
    public async Task CreateRabbitMqChannelAsync_WhenPublisherConfirmationIsEnabledForVirtualHost_ShouldCreateChannelWithPublisherConfirmation()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;
        var rabbitMqConnection = Substitute.For<IRabbitMqConnection>();
        rabbitMqConnection.CreatePublisherChannelAsync(Arg.Any<bool>(), cancellationToken)
            .Returns(Task.FromResult(Substitute.For<IChannel>()));
        _rabbitMqConnectionManager.GetOrCreateConnection(Arg.Any<RabbitMqHostSettings>())
            .Returns(rabbitMqConnection);
        var settings = CreatePublisherOptions(publisherConfirmation: true);

        // Act
        await _publisherCollector.CreateRabbitMqChannelAsync(settings, cancellationToken);

        // Assert
        await rabbitMqConnection.Received(1).CreatePublisherChannelAsync(publisherConfirmation: true, cancellationToken);
    }

    [Test]
    public async Task CreateRabbitMqChannelAsync_WhenPublisherConfirmationIsDisabledForVirtualHost_ShouldCreateChannelWithoutPublisherConfirmation()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;
        var rabbitMqConnection = Substitute.For<IRabbitMqConnection>();
        rabbitMqConnection.CreatePublisherChannelAsync(Arg.Any<bool>(), cancellationToken)
            .Returns(Task.FromResult(Substitute.For<IChannel>()));
        _rabbitMqConnectionManager.GetOrCreateConnection(Arg.Any<RabbitMqHostSettings>())
            .Returns(rabbitMqConnection);
        var settings = CreatePublisherOptions(publisherConfirmation: false);

        // Act
        await _publisherCollector.CreateRabbitMqChannelAsync(settings, cancellationToken);

        // Assert
        await rabbitMqConnection.Received(1).CreatePublisherChannelAsync(publisherConfirmation: false, cancellationToken);
    }

    #endregion
    
    #region GetPublishersInformation

    [Test]
    public void GetPublishersInformation_PublisherWithVirtualHostKey_ShouldReturnResolvedSettingsWithEventClass()
    {
        var virtualHostsSettings = new Dictionary<string, RabbitMqHostSettings>
        {
            {
                "TestVirtualHostKey", new RabbitMqHostSettings
                {
                    VirtualHost = "TestVirtualHost",
                    ExchangeName = "TestExchangeName",
                    ExchangeType = "topic",
                    UserName = "TestUser",
                    Password = "TestPassword"
                }
            }
        };
        _publisherCollector.AddPublisher(typeof(SimplePublishEvent),
            new EventPublisherOptions { VirtualHostKey = "TestVirtualHostKey" });
        _publisherCollector.SetVirtualHostAndOwnSettingsOfPublishers(virtualHostsSettings);

        var publishers = _publisherCollector.GetPublishersInformation();

        var publisher = publishers.Single();
        Assert.That(publisher.Handler.EventTypeName, Is.EqualTo(nameof(SimplePublishEvent)));
        Assert.That(publisher.Handler.EventTypeNamespace, Is.EqualTo(typeof(SimplePublishEvent).Namespace));
        Assert.That(publisher.EventTypeName, Is.EqualTo(nameof(SimplePublishEvent)));
        Assert.That(publisher.RoutingKey, Is.EqualTo($"TestExchangeName.{nameof(SimplePublishEvent)}"));
        Assert.That(publisher.VirtualHostKey, Is.EqualTo("TestVirtualHostKey"));
        Assert.That(publisher.VirtualHostSettings.VirtualHost, Is.EqualTo("TestVirtualHost"));
        Assert.That(publisher.VirtualHostSettings.ExchangeName, Is.EqualTo("TestExchangeName"));
        Assert.That(publisher.VirtualHostSettings.ExchangeType, Is.EqualTo("topic"));
    }

    [Test]
    public void GetPublishersInformation_OutboxIsNotUsed_ShouldReturnPublisherManagerAsHandler()
    {
        _publisherCollector.AddPublisher<SimplePublishEvent>();
        _publisherCollector.SetMessageBrokerEventPublishers(new Dictionary<Type, Type>
        {
            { typeof(SimplePublishEvent), typeof(SimplePublishEventPublisher) }
        });

        var publishers = _publisherCollector.GetPublishersInformation();

        var expectedHandler = EventHandlerInformation.Create(typeof(SimplePublishEvent), typeof(EventPublisherManager));
        Assert.That(publishers.Single().Handler, Is.EqualTo(expectedHandler));
    }

    [Test]
    public void GetPublishersInformation_OutboxIsUsedAndEventHasSpecificPublisher_ShouldReturnSpecificPublisherAsHandler()
    {
        var publisherCollector = CreatePublisherCollectorWithOutbox();
        publisherCollector.AddPublisher<SimplePublishEvent>();
        publisherCollector.SetMessageBrokerEventPublishers(new Dictionary<Type, Type>
        {
            { typeof(SimplePublishEvent), typeof(SimplePublishEventPublisher) }
        });

        var publishers = publisherCollector.GetPublishersInformation();

        var expectedHandler =
            EventHandlerInformation.Create(typeof(SimplePublishEvent), typeof(SimplePublishEventPublisher));
        Assert.That(publishers.Single().Handler, Is.EqualTo(expectedHandler));
    }

    [Test]
    public void GetPublishersInformation_OutboxIsUsedAndEventHasNoSpecificPublisher_ShouldReturnGlobalPublisherAsHandler()
    {
        var publisherCollector = CreatePublisherCollectorWithOutbox();
        publisherCollector.AddPublisher<SimplePublishEvent>();
        publisherCollector.SetMessageBrokerEventPublishers(new Dictionary<Type, Type>());

        var publishers = publisherCollector.GetPublishersInformation();

        var expectedHandler =
            EventHandlerInformation.Create(typeof(SimplePublishEvent), typeof(MessageBrokerEventPublisher));
        Assert.That(publishers.Single().Handler, Is.EqualTo(expectedHandler));
    }

    [Test]
    public void GetPublishersInformation_SettingsAreNotResolvedYet_ShouldReturnPublisherWithoutVirtualHostSettings()
    {
        _publisherCollector.AddPublisher<SimplePublishEvent>();

        var publishers = _publisherCollector.GetPublishersInformation();

        Assert.That(publishers.Single().VirtualHostSettings, Is.Null);
    }

    #endregion

    #region Helper methods

    private static EventPublisherOptions CreatePublisherOptions(bool? publisherConfirmation)
    {
        var publisherOptions = new EventPublisherOptions();
        publisherOptions.SetVirtualHostAndUnassignedSettings(new RabbitMqHostSettings
        {
            VirtualHost = "TestVirtualHost",
            ExchangeName = "TestExchangeName",
            PublisherConfirmation = publisherConfirmation
        }, nameof(SimplePublishEvent));

        return publisherOptions;
    }
    
    private static EventPublisherOptions CreatePublisherOptions(string exchangeName)
    {
        var publisherOptions = new EventPublisherOptions();
        publisherOptions.SetVirtualHostAndUnassignedSettings(new RabbitMqHostSettings
        {
            VirtualHost = "TestVirtualHost",
            ExchangeName = exchangeName,
            ExchangeType = "topic"
        }, nameof(SimplePublishEvent));

        return publisherOptions;
    }

    /// <summary>
    /// Sets up the connection of every publisher to return the given channel creation result.
    /// </summary>
    private IRabbitMqConnection SetupPublisherChannel(Task<IChannel> channelCreation)
    {
        var rabbitMqConnection = Substitute.For<IRabbitMqConnection>();
        rabbitMqConnection.CreatePublisherChannelAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(channelCreation);
        _rabbitMqConnectionManager.GetOrCreateConnection(Arg.Any<RabbitMqHostSettings>())
            .Returns(rabbitMqConnection);

        return rabbitMqConnection;
    }

    private bool HasErrorLog()
    {
        return _logger.ReceivedCalls().Any(call =>
            call.GetMethodInfo().Name == nameof(ILogger.Log) &&
            call.GetArguments()[0] is LogLevel.Error);
    }

    private EventPublisherCollector CreatePublisherCollectorWithOutbox()
    {
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(RabbitMqOptions)).Returns(new RabbitMqOptions { UseOutbox = true });
        serviceProvider.GetService(typeof(ILogger<EventPublisherCollector>)).Returns(_logger);
        serviceProvider.GetService(typeof(IRabbitMqConnectionManager)).Returns(_rabbitMqConnectionManager);

        return new EventPublisherCollector(serviceProvider);
    }

    private static readonly FieldInfo PublishersField = typeof(EventPublisherCollector)
        .GetField("_publishers", BindingFlags.NonPublic | BindingFlags.Instance);
    
    Dictionary<string, PublisherInformation> GetPublishersInfo()
    {
        var publishers = (Dictionary<string, PublisherInformation>)PublishersField?.GetValue(_publisherCollector)!;
        return publishers;
    }

    private static PublisherInformation CreatePublisherInformation(string exchangeName)
    {
        return new PublisherInformation
        {
            EventType = typeof(SimplePublishEvent),
            Settings = CreatePublisherOptions(exchangeName)
        };
    }

    #endregion
}
