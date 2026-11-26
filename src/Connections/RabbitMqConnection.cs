using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using EventBus.RabbitMQ.Configurations;
using EventBus.RabbitMQ.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace EventBus.RabbitMQ.Connections;

internal class RabbitMqConnection : IRabbitMqConnection
{
    public bool IsConnected => _connection?.IsOpen == true && !_disposed;
    
    /// <summary>
    /// Guards <see cref="_connectionId"/>: a <see cref="Guid"/> is not read and written in one step (atomically),
    /// so without the lock another thread could see half of the old ID and half of the new one.
    /// </summary>
    private readonly Lock _connectionIdLock = new();
    
    private Guid _connectionId = Guid.Empty;
    public Guid ConnectionId
    {
        get
        {
            lock (_connectionIdLock)
                return _connectionId;
        }
    }

    public event EventHandler Reconnected;

    /// <summary>
    /// 1 while <see cref="RetryReconnectAsync"/> is running, otherwise 0, so only one retry loop is started.
    /// An <see cref="int"/> instead of a <see cref="bool"/>, to be used with <see cref="Interlocked"/>.
    /// </summary>
    private int _isRetryingToReconnect;

    /// <summary>
    /// Used only for a temporary channel to verify that the RabbitMQ connection
    /// is actually open and usable before marking it as connected.
    /// </summary>
    private static readonly CreateChannelOptions TemporaryChannelOptions = new(
        publisherConfirmationsEnabled: false,
        publisherConfirmationTrackingEnabled: false,
        outstandingPublisherConfirmationsRateLimiter: null,
        consumerDispatchConcurrency: null
    );

    private readonly IConnectionFactory _connectionFactory;
    private readonly RabbitMqHostSettings _connectionOptions;
    private readonly ILogger<RabbitMqConnection> _logger;
    private IConnection _connection;
    private readonly SemaphoreSlim _connectionGate = new(1, 1);

    public RabbitMqConnection(RabbitMqHostSettings virtualHostSettings, IServiceProvider serviceProvider)
    {
        _connectionOptions = virtualHostSettings;
        _logger = serviceProvider.GetRequiredService<ILogger<RabbitMqConnection>>();
        _connectionFactory = CreateConnectionFactory();
    }

    #region TryConnect

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(RabbitMqConnection));

        var isReconnected = false;
        await _connectionGate.WaitAsync(cancellationToken);
        try
        {
            if (_disposed) throw new ObjectDisposedException(nameof(RabbitMqConnection));
            if (IsConnected) return;

            IConnection newConnection = null;
            var newConnectionId = Guid.CreateVersion7();
            try
            {
                _logger.LogDebug(
                    "RabbitMQ Client is trying to connect to the '{VirtualHost}' virtual host of '{HostName}' RabbitMQ host",
                    _connectionOptions.VirtualHost, _connectionOptions.HostName);

                var policy = Policy.Handle<SocketException>()
                    .Or<BrokerUnreachableException>()
                    .WaitAndRetryAsync(_connectionOptions.RetryConnectionCount,
                        retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))
                    );

                var applicationName = AppDomain.CurrentDomain.FriendlyName;
                // The id lets you find this connection in the management UI by the id in the logs.
                var connectionDisplayName =
                    $"For the {_connectionOptions.VirtualHost} from the {applicationName} service ({newConnectionId})";

                newConnection = await policy.ExecuteAsync(
                    ct => _connectionFactory.CreateConnectionAsync(connectionDisplayName, ct),
                    cancellationToken);

                if (newConnection?.IsOpen != true)
                    throw new EventBusException(
                        $"The created RabbitMQ connection is not opened to the '{_connectionOptions.VirtualHost}' virtual host of '{_connectionOptions.HostName}'.");

                await EnsureConnectionIsOpened(newConnection, cancellationToken);
                newConnection.ConnectionShutdownAsync += OnConnectionShutdownAsync;
                newConnection.CallbackExceptionAsync += OnCallbackExceptionAsync;
                newConnection.ConnectionBlockedAsync += OnConnectionBlockedAsync;
            }
            catch (Exception e)
            {
                if (newConnection is not null)
                    DisposeConnection(newConnection);

                if (e is OperationCanceledException && cancellationToken.IsCancellationRequested)
                    throw;

                throw new EventBusException(e,
                    $"Error while opening connection to the '{_connectionOptions.VirtualHost}' virtual host of '{_connectionOptions.HostName}'.");
            }

            // Dispose the old connection only now, after the new one is opened (see OnConnectionShutdownAsync).
            var oldConnection = _connection;
            _connection = newConnection;
            lock (_connectionIdLock)
            {
                // Not the first connection, so it replaces a lost one.
                isReconnected = _connectionId != Guid.Empty;
                _connectionId = newConnectionId;
            }
            if (oldConnection is not null)
                DisposeConnection(oldConnection);

            _logger.LogInformation(
                "The RabbitMQ connection '{ConnectionId}' is opened on host '{HostName}:{HostPort}' with virtual host '{VirtualHost}'.",
                newConnectionId, _connectionOptions.HostName, _connectionOptions.HostPort,
                _connectionOptions.VirtualHost);
        }
        finally
        {
            _connectionGate.Release();
        }

        if (isReconnected)
            OnReconnected();
    }

    /// <summary>
    /// Raises <see cref="Reconnected"/>; errors of the handlers are logged, not thrown.
    /// </summary>
    private void OnReconnected()
    {
        try
        {
            Reconnected?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception e)
        {
            _logger.LogError(e,
                "Error while notifying about the reconnected RabbitMQ connection of the '{VirtualHost}' virtual host of '{HostName}'.",
                _connectionOptions.VirtualHost, _connectionOptions.HostName);
        }
    }

    #endregion

    #region Create channel

    public Task<IChannel> CreateConsumerChannelAsync(CancellationToken cancellationToken)
    {
        return CreateChannelAsync(
            publisherConfirmationsEnabled: false,
            publisherConfirmationTrackingEnabled: false,
            cancellationToken);
    }

    public Task<IChannel> CreatePublisherChannelAsync(bool publisherConfirmation,
        CancellationToken cancellationToken)
    {
        return CreateChannelAsync(
            publisherConfirmationsEnabled: publisherConfirmation,
            publisherConfirmationTrackingEnabled: publisherConfirmation,
            cancellationToken);
    }

    /// <summary>
    /// Creates a channel with the given publisher confirmation options after opening the connection.
    /// </summary>
    /// <param name="publisherConfirmationsEnabled">Whether the broker should acknowledge each published event.</param>
    /// <param name="publisherConfirmationTrackingEnabled">Whether the client should track the acknowledgments of the published events.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task<IChannel> CreateChannelAsync(
        bool publisherConfirmationsEnabled,
        bool publisherConfirmationTrackingEnabled,
        CancellationToken cancellationToken
    )
    {
        if (_disposed) throw new ObjectDisposedException(nameof(RabbitMqConnection));

        try
        {
            await ConnectAsync(cancellationToken);

            var connection = Volatile.Read(ref _connection);
            if (connection?.IsOpen != true)
                throw new EventBusException(
                    $"RabbitMQ connection is not opened yet to the '{_connectionOptions.VirtualHost}' virtual host of '{_connectionOptions.HostName}'.");

            var channelOptions = new CreateChannelOptions(
                publisherConfirmationsEnabled: publisherConfirmationsEnabled,
                publisherConfirmationTrackingEnabled: publisherConfirmationTrackingEnabled,
                outstandingPublisherConfirmationsRateLimiter: null,
                consumerDispatchConcurrency: null
            );

            return await connection.CreateChannelAsync(
                channelOptions,
                cancellationToken);
        }
        catch (IOException e)
        {
            throw new EventBusException(e,
                $"Error while creating a channel to the '{_connectionOptions.VirtualHost}' virtual host of '{_connectionOptions.HostName}'.");
        }
    }

    #endregion

    #region Connection event handlers

    /// <summary>
    /// The event handler for reconnecting when the connection is blocked
    /// </summary>
    private Task OnConnectionBlockedAsync(object sender, ConnectionBlockedEventArgs e)
    {
        StartReconnecting(sender as IConnection, closeOpenedConnection: true);
        return Task.CompletedTask;
    }

    /// <summary>
    /// The event handler for reconnecting when an exception is thrown
    /// </summary>
    private Task OnCallbackExceptionAsync(object sender, CallbackExceptionEventArgs e)
    {
        StartReconnecting(sender as IConnection, closeOpenedConnection: true);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Reconnects when the connection is lost.
    /// Only starts reconnecting in the background: the RabbitMQ client notifies the channels about the shutdown
    /// after this handler, and silently skips them if the connection is already disposed. So the connection must not
    /// be disposed (nor the client blocked) here, otherwise consumers are never recreated.
    /// </summary>
    private Task OnConnectionShutdownAsync(object sender, ShutdownEventArgs reason)
    {
        if (_disposed) return Task.CompletedTask;

        _logger.LogWarning(
            "The RabbitMQ connection '{ConnectionId}' to the '{VirtualHost}' virtual host of '{HostName}' RabbitMQ host is shut down. Reconnecting. Reason: {Reason}",
            ConnectionId, _connectionOptions.VirtualHost, _connectionOptions.HostName, reason);

        StartReconnecting(sender as IConnection, closeOpenedConnection: false);
        return Task.CompletedTask;
    }

    /// <summary>
    /// The delay between retry attempts when reconnecting fails.
    /// </summary>
    private static readonly TimeSpan ReconnectRetryDelay = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Reconnects in the background, so the RabbitMQ client's event handlers return immediately.
    /// </summary>
    /// <param name="connection">The connection that raised the event.</param>
    /// <param name="closeOpenedConnection">Close the connection first, even if it is still open (e.g. blocked).</param>
    private void StartReconnecting(IConnection connection, bool closeOpenedConnection)
    {
        if (_disposed) return;

        _ = Task.Run(() => ReconnectAsync(connection, closeOpenedConnection));
    }

    private async Task ReconnectAsync(IConnection connection, bool closeOpenedConnection)
    {
        if (_disposed) return;

        if (closeOpenedConnection)
        {
            await _connectionGate.WaitAsync(CancellationToken.None);
            try
            {
                if (_disposed) return;

                // Only if it is still the current one, not an already reconnected one.
                if (connection is null || ReferenceEquals(connection, _connection))
                    DisposeConnectionIfExists();
            }
            finally
            {
                _connectionGate.Release();
            }
        }

        try
        {
            await ConnectAsync(CancellationToken.None);
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception e)
        {
            _logger.LogError(e,
                "Error while reconnecting to the '{VirtualHost}' virtual host of '{HostName}' RabbitMQ host. Will keep retrying every {RetryDelay} until it succeeds.",
                _connectionOptions.VirtualHost, _connectionOptions.HostName, ReconnectRetryDelay);

            if (Interlocked.Exchange(ref _isRetryingToReconnect, 1) == 0)
                _ = RetryReconnectAsync();
        }
    }

    /// <summary>
    /// Keeps retrying to reconnect every <see cref="ReconnectRetryDelay"/> until it succeeds or the connection is disposed.
    /// </summary>
    private async Task RetryReconnectAsync()
    {
        try
        {
            while (!_disposed)
            {
                await Task.Delay(ReconnectRetryDelay);

                if (_disposed) break;
                if (IsConnected) return;

                try
                {
                    await ConnectAsync(CancellationToken.None);
                    _logger.LogInformation(
                        "Successfully reconnected to the '{VirtualHost}' virtual host of '{HostName}' RabbitMQ host after retrying.",
                        _connectionOptions.VirtualHost, _connectionOptions.HostName);
                    return;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception e)
                {
                    _logger.LogError(e,
                        "Retry failed while reconnecting to the '{VirtualHost}' virtual host of '{HostName}' RabbitMQ host. Will retry again in {RetryDelay}.",
                        _connectionOptions.VirtualHost, _connectionOptions.HostName, ReconnectRetryDelay);
                }
            }
        }
        finally
        {
            Interlocked.Exchange(ref _isRetryingToReconnect, 0);
        }
    }

    #endregion

    #region Helper methods

    /// <summary>
    /// Creates the connection factory based on the given connection options. If the UseTls option is enabled,
    /// it configures the SSL settings including loading the client certificate and key from the specified file paths.
    /// </summary>
    /// <returns>A configured instance of <see cref="ConnectionFactory"/>.</returns>
    private ConnectionFactory CreateConnectionFactory()
    {
        try
        {
            var connectionFactory = new ConnectionFactory
            {
                HostName = _connectionOptions.HostName,
                Port = _connectionOptions.HostPort!.Value,
                VirtualHost = _connectionOptions.VirtualHost,
                UserName = _connectionOptions.UserName,
                Password = _connectionOptions.Password
            };

            if (_connectionOptions.UseTls != true) return connectionFactory;

            if (string.IsNullOrEmpty(_connectionOptions.ClientCertPath))
                _logger.LogError(
                    "Using the UseTls (TLS protocol) is enabled for the {VirtualHost} virtual host of {HostName} host, but the ClientCertPath is not set.",
                    _connectionOptions.VirtualHost, _connectionOptions.HostName);

            if (string.IsNullOrEmpty(_connectionOptions.ClientKeyPath))
                _logger.LogError(
                    "Using the UseTls (TLS protocol) is enabled for the {VirtualHost} virtual host of {HostName} host, but the ClientKeyPath is not set.",
                    _connectionOptions.VirtualHost, _connectionOptions.HostName);

            var clientCertFullPath = GetFullPath(_connectionOptions.ClientCertPath);
            var clientKeyFullPath = GetFullPath(_connectionOptions.ClientKeyPath);
            connectionFactory.Ssl = new SslOption
            {
                Enabled = true,
                ServerName = _connectionOptions.HostName,
                CertificateValidationCallback = (_, _, _, _) => true,
                Version = _connectionOptions.SslProtocolVersion!.Value,
                Certs =
                [
                    X509Certificate2.CreateFromPemFile(clientCertFullPath, clientKeyFullPath)
                ]
            };

            return connectionFactory;
        }
        catch (Exception e)
        {
            throw new EventBusException(e,
                $"Error while creating the RabbitMQ connection factory for the {_connectionOptions.VirtualHost} virtual host of {_connectionOptions.HostName}.");
        }
    }

    /// <summary>
    /// Validates that the created RabbitMQ connection is actually usable by performing
    /// a real AMQP operation. We intentionally do not rely on <see cref="IConnection.IsOpen"/>
    /// here, because RabbitMQ client documentation notes that checking IsOpen/CloseReason
    /// is race-prone and does not guarantee the connection will remain usable.
    /// </summary>
    /// <param name="connection">The created RabbitMQ connection to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private static async Task EnsureConnectionIsOpened(IConnection connection, CancellationToken cancellationToken)
    {
        await using var channel = await connection.CreateChannelAsync(TemporaryChannelOptions, cancellationToken);
    }

    /// <summary>
    /// Get certificate file path from the relative path
    /// </summary>
    /// <param name="relativePath">The relative path to calculate based on that</param>
    /// <returns>Calculated full file path</returns>
    /// <exception cref="Exception">when the path is invalid</exception>
    private string GetFullPath(string relativePath)
    {
        var fullRelativePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativePath);
        var clientKeyPath = Path.GetFullPath(fullRelativePath);

        if (!File.Exists(clientKeyPath))
            throw new Exception($"The '{relativePath}' certificate file path of the RabbitMQ does not exist.");

        return clientKeyPath;
    }

    /// <summary>
    /// When something goes wrong with the connection, we want to dispose the old connection if exists
    /// to be able to create a new one.
    /// </summary>
    private void DisposeConnectionIfExists()
    {
        if (_connection == null) return;

        DisposeConnection(_connection);
        _connection = null;
    }

    /// <summary>
    /// Unsubscribes from the events of the given connection and disposes it.
    /// </summary>
    private void DisposeConnection(IConnection connection)
    {
        try
        {
            connection.ConnectionShutdownAsync -= OnConnectionShutdownAsync;
            connection.CallbackExceptionAsync -= OnCallbackExceptionAsync;
            connection.ConnectionBlockedAsync -= OnConnectionBlockedAsync;
        }
        catch (Exception ex)
        {
            // Unsubscribing from a disposed connection throws.
            _logger.LogDebug(ex, "Error while unsubscribing from the events of the old RabbitMQ connection.");
        }

        try
        {
            connection.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while disposing old RabbitMQ connection.");
        }
    }

    #endregion

    #region Dispose

    private bool _disposed;

    /// <summary>
    /// To close opened connection before disposing
    /// </summary>
    public void Dispose()
    {
        Disposing();
        GC.SuppressFinalize(this);
    }

    private void Disposing()
    {
        if (_disposed) return;

        _connectionGate.Wait();
        try
        {
            if (_disposed) return;

            DisposeConnectionIfExists();
            _disposed = true;
        }
        finally
        {
            _connectionGate.Release();
        }
    }

    ~RabbitMqConnection()
    {
        Disposing();
    }

    #endregion
}
