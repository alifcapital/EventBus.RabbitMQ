using EventBus.RabbitMQ.Configurations;
using EventBus.RabbitMQ.Connections;
using EventBus.RabbitMQ.Extensions;
using EventBus.RabbitMQ.Publishers.Managers;
using EventBus.RabbitMQ.Tests.Domain;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace EventBus.RabbitMQ.Tests.UnitTests.Extensions;

public class RabbitMqExtensionsTests : BaseTestEntity
{
    private IServiceProvider _serviceProvider;
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
    }

    #endregion

    #region GetPublisherTypes

    [Test]
    public void GetPublisherTypes_GettingJustCreatedOnThisProjectEvent_ShouldReturnOneExpectedType()
    {
        // Arrange
        var expectedTypes = new[] { typeof(SimplePublishEvent) };

        // Act
        var result = RabbitMqExtensions.GetPublisherTypes(
            [
                typeof(RabbitMqExtensionsTests).Assembly,
                typeof(EventPublisherCollector).Assembly
            ]
        );

        // Assert
        Assert.That(result, Is.EquivalentTo(expectedTypes));
    }

    #endregion

    #region GetSubscriberTypes

    [Test]
    public void GetSubscriberTypes_GettingJustCreatedOnThisProjectEvent_ShouldReturnOneExpectedTypeAndHandlerType()
    {
        var expectedTypes = new List<(Type eventType, Type handlerType)>
        {
            (typeof(SimpleSubscribeEvent), typeof(SimpleEventSubscriberHandler)),
            (typeof(Domain.Module1.UserCreated), typeof(Domain.Module1.UserCreatedSubscriber)),
            (typeof(Domain.Module2.UserCreated), typeof(Domain.Module2.UserCreatedSubscriber))
        };

        var result = RabbitMqExtensions.GetSubscriberReceiverTypes(
            [
                typeof(RabbitMqExtensionsTests).Assembly,
                typeof(EventPublisherCollector).Assembly
            ]
        );

        Assert.That(result, Is.EquivalentTo(expectedTypes));
    }

    #endregion
}