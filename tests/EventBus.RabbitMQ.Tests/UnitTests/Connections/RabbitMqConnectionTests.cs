using EventBus.RabbitMQ.Configurations;
using EventBus.RabbitMQ.Connections;
using EventBus.RabbitMQ.Exceptions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace EventBus.RabbitMQ.Tests.UnitTests.Connections;

public class RabbitMqConnectionTests : BaseTestEntity
{
    private RabbitMqConnection _connection;
    private RabbitMqHostSettings _connectionOptions;

    #region SetUp & TearDown

    [SetUp]
    public void Setup()
    {
        _connectionOptions = RabbitMqOptionsConstant.CreateDefaultRabbitMqOptions();
        _connectionOptions.RetryConnectionCount = 1;
        _connectionOptions.HostName = "127.0.0.1";
        _connectionOptions.HostPort = 1;
        
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
    public void Connect_WhenCannotOpenConnection_ShouldThrowEventBusException()
    {
        var exception = Assert.Throws<EventBusException>(() => _connection.Connect());

        var expectedMessage =
            $"Error while opening connection to the '{_connectionOptions.VirtualHost}' virtual host of '{_connectionOptions.HostName}'.";

        using (Assert.EnterMultipleScope())
        {
            Assert.That(exception!.Message, Is.EqualTo(expectedMessage));
            Assert.That(exception.InnerException, Is.Not.Null);
        }
    }

    #endregion
}