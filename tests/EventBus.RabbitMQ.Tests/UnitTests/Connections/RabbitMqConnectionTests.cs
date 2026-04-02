using System.Reflection;
using EventBus.RabbitMQ.Configurations;
using EventBus.RabbitMQ.Connections;
using EventBus.RabbitMQ.Exceptions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace EventBus.RabbitMQ.Tests.UnitTests.Connections;

public class RabbitMqConnectionTests : BaseTestEntity
{
    private RabbitMqConnection _connection;
    private RabbitMqHostSettings _connectionOptions;
    private ILogger<RabbitMqConnection> _logger;

    #region SetUp & TearDown

    [SetUp]
    public void Setup()
    {
        _connectionOptions = RabbitMqOptionsConstant.CreateDefaultRabbitMqOptions();
        _connectionOptions.RetryConnectionCount = 1;
        _connectionOptions.HostName = "127.0.0.1";
        _connectionOptions.HostPort = 1;

        var serviceProvider = Substitute.For<IServiceProvider>();
        _logger = Substitute.For<ILogger<RabbitMqConnection>>();
        serviceProvider.GetService(typeof(ILogger<RabbitMqConnection>)).Returns(_logger);
        _connection = Substitute.ForPartsOf<RabbitMqConnection>(_connectionOptions, serviceProvider);
    }

    [TearDown]
    public void TearDown()
    {
        _connection.Dispose();
    }

    #endregion

    #region ConnectAsync

    [Test]
    public async Task ConnectAsync_WhenCannotOpenConnection_ShouldThrowEventBusException()
    {
        var exception = Assert.ThrowsAsync<EventBusException>(async () => await _connection.ConnectAsync(CancellationToken.None));

        var expectedMessage =
            $"Error while opening connection to the '{_connectionOptions.VirtualHost}' virtual host of '{_connectionOptions.HostName}'.";

        using (Assert.EnterMultipleScope())
        {
            Assert.That(exception!.Message, Is.EqualTo(expectedMessage));
            Assert.That(exception.InnerException, Is.Not.Null);
        }
    }

    [Test]
    public void ConnectAsync_WhenVirtualHostIsInvalid_ShouldThrowEventBusExceptionWithoutSuccessLog()
    {
        var connectionFactory = Substitute.For<IConnectionFactory>();
        var invalidVhostConnection = CreateOpenedConnection(_ =>
        {
            var shutdownReason = new ShutdownEventArgs(ShutdownInitiator.Peer, 530,
                "NOT_ALLOWED - access to vhost refused", new IOException("access denied"));
            return Task.FromException<IChannel>(new AlreadyClosedException(shutdownReason));
        });
        connectionFactory.CreateConnectionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(invalidVhostConnection));
        SetPrivateField("_connectionFactory", connectionFactory);

        var exception = Assert.ThrowsAsync<EventBusException>(async () => await _connection.ConnectAsync(CancellationToken.None));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(exception!.InnerException, Is.TypeOf<AlreadyClosedException>());
            Assert.That(HasLogContaining(LogLevel.Debug, "RabbitMQ Client is trying to connect"), Is.True);
            Assert.That(HasLogContaining(LogLevel.Information, "The RabbitMQ connection is opened"), Is.False);
        }
    }

    #endregion

    #region Helpers

    private static IConnection CreateOpenedConnection(Func<CreateChannelOptions, Task<IChannel>> channelFactory)
    {
        var connection = Substitute.For<IConnection>();
        connection.IsOpen.Returns(true);
        connection.CreateChannelAsync(Arg.Any<CreateChannelOptions>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => channelFactory((CreateChannelOptions)callInfo[0]!));

        return connection;
    }

    private void SetPrivateField(string fieldName, object value)
    {
        var field = typeof(RabbitMqConnection).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null);
        field!.SetValue(_connection, value);
    }

    private bool HasLogContaining(LogLevel level, string message)
    {
        return _logger.ReceivedCalls().Any(call =>
        {
            if (call.GetMethodInfo().Name != nameof(ILogger.Log))
                return false;

            var arguments = call.GetArguments();
            return arguments.Length >= 3 &&
                   arguments[0] is LogLevel logLevel &&
                   logLevel == level &&
                   arguments[2]?.ToString()?.Contains(message, StringComparison.Ordinal) == true;
        });
    }

    #endregion
}
