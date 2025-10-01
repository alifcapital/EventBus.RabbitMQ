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

internal sealed class RabbitMqConnection : IRabbitMqConnection
{
    public bool IsConnected => _connection?.IsOpen == true && !_disposed;

    public int RetryConnectionCount { get; }

    private readonly IConnectionFactory _connectionFactory;
    private readonly RabbitMqHostSettings _connectionOptions;
    private readonly ILogger<RabbitMqConnection> _logger;
    private IConnection _connection;

    public RabbitMqConnection(RabbitMqHostSettings virtualHostSettings, IServiceProvider serviceProvider)
    {
        _connectionOptions = virtualHostSettings;
        var connectionFactory = new ConnectionFactory
        {
            HostName = _connectionOptions.HostName,
            Port = _connectionOptions.HostPort!.Value,
            VirtualHost = _connectionOptions.VirtualHost,
            UserName = _connectionOptions.UserName,
            Password = _connectionOptions.Password,
            DispatchConsumersAsync = true
        };

        _logger = serviceProvider.GetRequiredService<ILogger<RabbitMqConnection>>();
        if (_connectionOptions.UseTls == true)
        {
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
        }
        
        _connectionFactory = connectionFactory;
        RetryConnectionCount = (int)_connectionOptions.RetryConnectionCount!;
    }

    private readonly Lock _lockOpenConnection = new();

    public bool TryConnect()
    {
        lock (_lockOpenConnection)
        {
            if (IsConnected) return true;

            _logger.LogTrace(
                "RabbitMQ Client is trying to connect to the {VirtualHost} virtual host of {HostName} RabbitMQ host",
                _connectionOptions.VirtualHost, _connectionOptions.HostName);

            var policy = Policy.Handle<SocketException>()
                .Or<BrokerUnreachableException>()
                .WaitAndRetry(RetryConnectionCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    (ex, time) =>
                    {
                        _logger.LogWarning(ex,
                            "RabbitMQ client could not connect to the {VirtualHost} virtual host of {HostName} RabbitMQ host after {TimeOut}s ({ExceptionMessage})",
                            _connectionOptions.VirtualHost, _connectionOptions.HostName, $"{time.TotalSeconds:n1}",
                            ex.Message);
                    }
                );

            var applicationName = AppDomain.CurrentDomain.FriendlyName;
            var connectionDisplayName =
                $"For the {_connectionOptions.VirtualHost} virtual host from the {applicationName} service";
            policy.Execute(() => { _connection = _connectionFactory.CreateConnection(connectionDisplayName); });

            if (IsConnected)
            {
                _connection!.ConnectionShutdown += OnConnectionShutdown;
                _connection!.CallbackException += OnCallbackException;
                _connection!.ConnectionBlocked += OnConnectionBlocked;

                _logger.LogInformation("The RabbitMQ connection is opened for an event subscribers or publishers on host '{HostName}:{HostPort}' with virtual host '{VirtualHost}'.",
                    _connectionOptions.HostName, _connectionOptions.HostPort, _connectionOptions.VirtualHost);

                return true;
            }

            _logger.LogCritical(
                "FATAL ERROR: Connection to the {VirtualHost} virtual host of {HostName} RabbitMQ host could not be opened",
                _connectionOptions.VirtualHost, _connectionOptions.HostName);
            return false;
        }
    }

    public IModel CreateChannel()
    {
        TryConnect();

        if (!IsConnected)
            throw new EventBusException(
                $"RabbitMQ connection is not opened yet to the {_connectionOptions.VirtualHost} virtual host of {_connectionOptions.HostName}.");

        return _connection.CreateModel();
    }

    #region Connection event handlers

    /// <summary>
    /// The event handler for reconnecting when the connection is blocked
    /// </summary>
    private void OnConnectionBlocked(object sender, ConnectionBlockedEventArgs e)
    {
        if (_disposed) return;

        DisposeConnectionIfExists();
        TryConnect();
    }

    /// <summary>
    /// The event handler for reconnecting when an exception is thrown
    /// </summary>
    private void OnCallbackException(object sender, CallbackExceptionEventArgs e)
    {
        if (_disposed) return;

        DisposeConnectionIfExists();
        TryConnect();
    }

    /// <summary>
    /// The event handler for reconnecting when the connection is shutdown
    /// </summary>
    private void OnConnectionShutdown(object sender, ShutdownEventArgs reason)
    {
        if (_disposed) return;

        DisposeConnectionIfExists();
        TryConnect();
    }

    #endregion

    #region Helper methods
    
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
            _connection.ConnectionShutdown -= OnConnectionShutdown;
            _connection.CallbackException -= OnCallbackException;
            _connection.ConnectionBlocked -= OnConnectionBlocked;

            _connection.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disposing old RabbitMQ connection.");
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

        try
        {
            DisposeConnectionIfExists();

            _disposed = true;
        }
        catch (IOException ex)
        {
            _logger.LogCritical(ex.ToString());
        }
    }

    ~RabbitMqConnection()
    {
        Disposing();
    }

    #endregion
}