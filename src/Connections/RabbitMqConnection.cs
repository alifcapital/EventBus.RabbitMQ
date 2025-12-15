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

    private readonly IConnectionFactory _connectionFactory;
    private readonly RabbitMqHostSettings _connectionOptions;
    private readonly ILogger<RabbitMqConnection> _logger;
    private IConnection _connection;

    public RabbitMqConnection(RabbitMqHostSettings virtualHostSettings, IServiceProvider serviceProvider,
        IConnectionFactory connectionFactory = null)
    {
        _connectionOptions = virtualHostSettings;
        _logger = serviceProvider.GetRequiredService<ILogger<RabbitMqConnection>>();
        _connectionFactory = connectionFactory ?? CreateConnectionFactory();
    }

    #region TryConnect

    private readonly Lock _lockOpenConnection = new();

    public void Connect()
    {
        lock (_lockOpenConnection)
        {
            if (IsConnected) return;

            try
            {
                _logger.LogDebug(
                    "RabbitMQ Client is trying to connect to the '{VirtualHost} 'virtual host of '{HostName}' RabbitMQ host",
                    _connectionOptions.VirtualHost, _connectionOptions.HostName);

                var policy = Policy.Handle<SocketException>()
                    .Or<BrokerUnreachableException>()
                    .WaitAndRetry(_connectionOptions.RetryConnectionCount,
                        retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))
                    );

                var applicationName = AppDomain.CurrentDomain.FriendlyName;
                var connectionDisplayName =
                    $"For the {_connectionOptions.VirtualHost} from the {applicationName} service";
                policy.Execute(() =>
                {
                    _connection = _connectionFactory
                        .CreateConnectionAsync(connectionDisplayName, CancellationToken.None)
                        .GetAwaiter().GetResult();
                });

                if (IsConnected)
                {
                    _connection!.ConnectionShutdownAsync += OnConnectionShutdownAsync;
                    _connection!.CallbackExceptionAsync += OnCallbackExceptionAsync;
                    _connection!.ConnectionBlockedAsync += OnConnectionBlockedAsync;

                    _logger.LogInformation(
                        "The RabbitMQ connection is opened on host '{HostName}:{HostPort}' with virtual host '{VirtualHost}'.",
                        _connectionOptions.HostName, _connectionOptions.HostPort, _connectionOptions.VirtualHost);
                }
            }
            catch (Exception e)
            {
                throw new EventBusException(e,
                    $"Error while opening connection to the '{_connectionOptions.VirtualHost}' virtual host of '{_connectionOptions.HostName}'.");
            }
        }
    }

    #endregion

    #region Create channel

    public IChannel CreateChannel()
    {
        try
        {
            Connect();

            if (!IsConnected)
                throw new EventBusException(
                    $"RabbitMQ connection is not opened yet to the '{_connectionOptions.VirtualHost}' virtual host of '{_connectionOptions.HostName}'.");

            return _connection
                .CreateChannelAsync(new CreateChannelOptions(false, false, null, null), CancellationToken.None)
                .GetAwaiter().GetResult();
        }
        catch (IOException e)
        {
            throw new EventBusException(e,
                $"Error while creating a channel to the '{_connectionOptions.VirtualHost}' virtual host of '{_connectionOptions.HostName}'.");
        }   
    }

    #endregion

    #region Connection event handlers

    private readonly Lock _lockReOpenConnection = new();

    /// <summary>
    /// The event handler for reconnecting when the connection is blocked
    /// </summary>
    private Task OnConnectionBlockedAsync(object sender, ConnectionBlockedEventArgs e)
    {
        if (_disposed) return Task.CompletedTask;

        lock (_lockReOpenConnection)
        {
            DisposeConnectionIfExists();
            Connect();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// The event handler for reconnecting when an exception is thrown
    /// </summary>
    private Task OnCallbackExceptionAsync(object sender, CallbackExceptionEventArgs e)
    {
        if (_disposed) return Task.CompletedTask;

        lock (_lockReOpenConnection)
        {
            DisposeConnectionIfExists();
            Connect();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// The event handler for reconnecting when the connection is shutdown
    /// </summary>
    private Task OnConnectionShutdownAsync(object sender, ShutdownEventArgs reason)
    {
        if (_disposed) return Task.CompletedTask;

        lock (_lockReOpenConnection)
        {
            DisposeConnectionIfExists();
            Connect();
        }

        return Task.CompletedTask;
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

        try
        {
            _connection.ConnectionShutdownAsync -= OnConnectionShutdownAsync;
            _connection.CallbackExceptionAsync -= OnCallbackExceptionAsync;
            _connection.ConnectionBlockedAsync -= OnConnectionBlockedAsync;

            _connection.Dispose();
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Error while disposing old RabbitMQ connection.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while disposing old RabbitMQ connection.");
        }

        _connection = null;
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

        DisposeConnectionIfExists();
        _disposed = true;
    }

    ~RabbitMqConnection()
    {
        Disposing();
    }

    #endregion
}