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
        var exception =
            await Assert.ThrowsAsync<EventBusException>(async () =>
                await _connection.ConnectAsync(CancellationToken.None));

        var expectedMessage =
            $"Error while opening connection to the '{_connectionOptions.VirtualHost}' virtual host of '{_connectionOptions.HostName}'.";

        using (Assert.EnterMultipleScope())
        {
            Assert.That(exception!.Message, Is.EqualTo(expectedMessage));
            Assert.That(exception.InnerException, Is.Not.Null);
        }
    }

    [Test]
    public async Task ConnectAsync_WhenVirtualHostIsInvalid_ShouldThrowEventBusExceptionWithoutSuccessLog()
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

        var exception =
            await Assert.ThrowsAsync<EventBusException>(async () =>
                await _connection.ConnectAsync(CancellationToken.None));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(exception!.InnerException, Is.TypeOf<AlreadyClosedException>());
            Assert.That(HasLogContaining(LogLevel.Debug, "RabbitMQ Client is trying to connect"), Is.True);
            Assert.That(HasLogContaining(LogLevel.Information, "is opened on host"), Is.False);
            Assert.That(_connection.ConnectionId, Is.EqualTo(Guid.Empty));
        }
    }

    [Test]
    public async Task ConnectAsync_OpeningFirstConnection_ShouldAssignConnectionIdWithoutRaisingReconnected()
    {
        var openedConnection = CreateOpenedConnection(_ => Task.FromResult(Substitute.For<IChannel>()));
        var connectionFactory = SetupConnectionFactory(openedConnection);
        var isReconnectedRaised = false;
        _connection.Reconnected += (_, _) => isReconnectedRaised = true;

        await _connection.ConnectAsync(CancellationToken.None);

        var connectionId = _connection.ConnectionId;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(connectionId, Is.Not.EqualTo(Guid.Empty));
            Assert.That(isReconnectedRaised, Is.False);
            Assert.That(HasLogContaining(LogLevel.Information, connectionId.ToString()), Is.True);
        }
        // The id is a part of the connection name, to find the connection in the management UI of RabbitMQ.
        await connectionFactory.Received(1)
            .CreateConnectionAsync(Arg.Is<string>(name => name.Contains(connectionId.ToString())),
                Arg.Any<CancellationToken>());
    }

    #endregion

    #region OnConnectionShutdownAsync

    [Test]
    public async Task OnConnectionShutdownAsync_WhenConnectionIsLost_ShouldNotDisposeConnectionWhileHandlingTheEvent()
    {
        var lostConnection = CreateOpenedConnection(_ => Task.FromResult(Substitute.For<IChannel>()));
        var newConnectionCreating = new TaskCompletionSource<IConnection>();
        SetupConnectionFactory(lostConnection, newConnectionCreating.Task);
        await _connection.ConnectAsync(CancellationToken.None);
        lostConnection.IsOpen.Returns(false);

        await InvokeConnectionShutdownAsync(lostConnection);

        // The RabbitMQ client notifies the channels after this handler, so the connection must still be usable by them.
        lostConnection.DidNotReceive().Dispose();
        var openedConnection = CreateOpenedConnection(_ => Task.FromResult(Substitute.For<IChannel>()));
        newConnectionCreating.SetResult(openedConnection);
    }

    [Test]
    public async Task OnConnectionShutdownAsync_WhenConnectionIsLost_ShouldOpenNewConnectionBeforeDisposingOldOne()
    {
        var lostConnection = CreateOpenedConnection(_ => Task.FromResult(Substitute.For<IChannel>()));
        var newConnection = CreateOpenedConnection(_ => Task.FromResult(Substitute.For<IChannel>()));
        var connectionFactory = SetupConnectionFactory(lostConnection, Task.FromResult(newConnection));
        await _connection.ConnectAsync(CancellationToken.None);
        var lostConnectionId = _connection.ConnectionId;
        lostConnection.IsOpen.Returns(false);
        var reconnected = new TaskCompletionSource();
        _connection.Reconnected += (_, _) => reconnected.TrySetResult();

        await InvokeConnectionShutdownAsync(lostConnection);

        await reconnected.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Received.InOrder(() =>
        {
            connectionFactory.CreateConnectionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
            connectionFactory.CreateConnectionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
            lostConnection.Dispose();
        });
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_connection.ConnectionId, Is.Not.EqualTo(lostConnectionId));
            Assert.That(_connection.ConnectionId, Is.Not.EqualTo(Guid.Empty));
            Assert.That(_connection.IsConnected, Is.True);
        }
    }

    [Test]
    public async Task OnConnectionShutdownAsync_AfterDisposing_ShouldNotReconnect()
    {
        var lostConnection = CreateOpenedConnection(_ => Task.FromResult(Substitute.For<IChannel>()));
        var connectionFactory = SetupConnectionFactory(lostConnection);
        await _connection.ConnectAsync(CancellationToken.None);
        _connection.Dispose();

        await InvokeConnectionShutdownAsync(lostConnection);

        await Task.Delay(TimeSpan.FromMilliseconds(300));
        await connectionFactory.Received(1).CreateConnectionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    #endregion

    #region CreatePublisherChannelAsync

    [Test]
    public async Task
        CreatePublisherChannelAsync_PassingEnabledPublisherConfirmation_ShouldCreateChannelWithEnabledPublisherConfirmation()
    {
        var createdChannelOptions = CreateOpenedConnectionForCreatingChannel();

        await _connection.CreatePublisherChannelAsync(publisherConfirmation: true, CancellationToken.None);

        var channelOptions = createdChannelOptions.Last();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(channelOptions.PublisherConfirmationsEnabled, Is.True);
            Assert.That(channelOptions.PublisherConfirmationTrackingEnabled, Is.True);
        }
    }

    [Test]
    public async Task
        CreatePublisherChannelAsync_PassingDisabledPublisherConfirmation_ShouldCreateChannelWithDisabledPublisherConfirmation()
    {
        var createdChannelOptions = CreateOpenedConnectionForCreatingChannel();

        await _connection.CreatePublisherChannelAsync(publisherConfirmation: false, CancellationToken.None);

        var channelOptions = createdChannelOptions.Last();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(channelOptions.PublisherConfirmationsEnabled, Is.False);
            Assert.That(channelOptions.PublisherConfirmationTrackingEnabled, Is.False);
        }
    }

    #endregion

    #region CreateConsumerChannelAsync

    [Test]
    public async Task
        CreateConsumerChannelAsync_CreatingChannelForConsumer_ShouldCreateChannelWithDisabledPublisherConfirmation()
    {
        var createdChannelOptions = CreateOpenedConnectionForCreatingChannel();

        await _connection.CreateConsumerChannelAsync(CancellationToken.None);

        var channelOptions = createdChannelOptions.Last();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(channelOptions.PublisherConfirmationsEnabled, Is.False);
            Assert.That(channelOptions.PublisherConfirmationTrackingEnabled, Is.False);
        }
    }

    #endregion

    #region Helpers

    private List<CreateChannelOptions> CreateOpenedConnectionForCreatingChannel()
    {
        var createdChannelOptions = new List<CreateChannelOptions>();
        var connectionFactory = Substitute.For<IConnectionFactory>();
        var openedConnection = CreateOpenedConnection(options =>
        {
            createdChannelOptions.Add(options);
            return Task.FromResult(Substitute.For<IChannel>());
        });
        connectionFactory.CreateConnectionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(openedConnection));
        SetPrivateField("_connectionFactory", connectionFactory);

        return createdChannelOptions;
    }

    /// <summary>
    /// Sets up the connection factory to return the given connection first, and then the given next connections.
    /// </summary>
    private IConnectionFactory SetupConnectionFactory(IConnection firstConnection,
        params Task<IConnection>[] nextConnections)
    {
        var connectionFactory = Substitute.For<IConnectionFactory>();
        connectionFactory.CreateConnectionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(firstConnection), nextConnections);
        SetPrivateField("_connectionFactory", connectionFactory);

        return connectionFactory;
    }

    /// <summary>
    /// Invokes the handler of the shutdown event of the given connection, as the RabbitMQ client does when it is lost.
    /// </summary>
    private async Task InvokeConnectionShutdownAsync(IConnection connection)
    {
        var method = typeof(RabbitMqConnection)
            .GetMethod("OnConnectionShutdownAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(method, Is.Not.Null);
        var reason = new ShutdownEventArgs(ShutdownInitiator.Peer, 320,
            "CONNECTION_FORCED - Closed via management plugin");

        await (Task)method!.Invoke(_connection, [connection, reason])!;
    }

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