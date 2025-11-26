using EventBus.RabbitMQ.Configurations;
using EventBus.RabbitMQ.Connections;
using EventBus.RabbitMQ.Exceptions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace EventBus.RabbitMQ.Tests.UnitTests.Connections;

public class RabbitMqConnectionTests : BaseTestEntity
{
    private RabbitMqConnection _connection;
    private RabbitMqOptions _connectionOptions;

    #region SetUp

    [SetUp]
    public void Setup()
    {
        _connectionOptions = RabbitMqOptionsConstant.CreateDefaultRabbitMqOptions();
        _connectionOptions.RetryConnectionCount = 1;
        var serviceProvider = Substitute.For<IServiceProvider>();
        var logger = Substitute.For<ILogger<RabbitMqConnection>>();
        serviceProvider.GetService(typeof(ILogger<RabbitMqConnection>)).Returns(logger);
        _connection = Substitute.ForPartsOf<RabbitMqConnection>(_connectionOptions, serviceProvider);
    }

    [TearDown]
    public void TearDown()
    {
        _connection.Dispose();
    }

    #endregion

    #region Connect

    [Test]
    public void Connect_OpeningConnectionWithoutConfiguringRabbitMQ_ShouldThrowEventBusException()
    {
        var exception = Assert.Throws<EventBusException>(() => _connection.Connect());

        var expectedMessage =
            $"Error while opening connection to the '{_connectionOptions.VirtualHost}' virtual host of '{_connectionOptions.HostName}'.";
        Assert.That(exception!.Message, Is.EqualTo(expectedMessage));
    }

    #endregion
}