using EventBus.RabbitMQ.Publishers.Managers;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace EventBus.RabbitMQ.Tests.UnitTests.Publishers;

public class EventPublisherManagerTests : BaseTestEntity
{
    private IEventPublisherCollector _publisherCollector;
    private EventPublisherManager _publisherManager;

    #region SetUp

    [SetUp]
    public void Setup()
    {
        var logger = Substitute.For<ILogger<EventPublisherManager>>();
        _publisherCollector = Substitute.For<IEventPublisherCollector>();
        _publisherManager = new EventPublisherManager(logger, _publisherCollector);
    }
    
    [TearDown]
    public void TearDown()
    {
        _publisherManager.Dispose();
    }

    #endregion
    
    #region Publish

    //TODO
    // [Test]
    // public async Task Publish_PublishingWithOptionsWithRoutingKey_ShouldCreatedConnection()
    // {
    //     // Arrange
    //     var options = new Action<EventPublisherOptions>(x => { x.RoutingKey = "TestRoutingKey"; });
    //     _publisherCollector.AddPublisher<SimplePublishEvent>(options);
    //     var virtualHostsSettings = new Dictionary<string, RabbitMqHostSettings>
    //     {
    //         {
    //             "TestVirtualHostKey", new RabbitMqHostSettings
    //             {
    //                 VirtualHost = "TestVirtualHost",
    //                 ExchangeName = "TestExchangeName"
    //             }
    //         }
    //     };
    //     _publisherCollector.SetVirtualHostAndOwnSettingsOfPublishers(virtualHostsSettings);
    //     _serviceProvider.GetService(typeof(ILogger<RabbitMqConnection>))
    //         .Returns(Substitute.For<ILogger<RabbitMqConnection>>());
    //
    //     _publisherCollector.CreateExchangeForPublishers();
    //     var @event = new SimplePublishEvent
    //     {
    //         Id = Guid.NewGuid(),
    //         Name = "TestName"
    //     };
    //
    //     // Act
    //     await _publisherCollector.PublishAsync(@event);
    //
    //     // Assert
    //     var field = _publisherCollector.GetType()
    //         .GetField("_openedRabbitMqConnections", BindingFlags.NonPublic | BindingFlags.Instance);
    //     field.Should().NotBeNull();
    //     var openedRabbitMqConnections = (Dictionary<string, IRabbitMqConnection>)field?.GetValue(_publisherCollector)!;
    //
    //     openedRabbitMqConnections?.Count.Should().Be(1);
    // }

    #endregion
}