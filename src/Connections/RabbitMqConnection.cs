using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using EventBus.RabbitMQ.Configurations;
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
    private static string _connectTitle;

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

        _connectTitle =
            $"The RabbitMQ connection is opened for an event subscriber or publisher for the '{_connectionOptions.HostName}':{_connectionOptions.HostPort} host's '{_connectionOptions.VirtualHost}' virtual host.";
    }

    readonly Lock _lockOpenConnection = new();

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

            policy.Execute(() => { _connection = _connectionFactory.CreateConnection(); });

            if (IsConnected && _connection is not null)
            {
                _connection.ConnectionShutdown += OnConnectionShutdown;
                _connection.CallbackException += OnCallbackException;
                _connection.ConnectionBlocked += OnConnectionBlocked;

                _logger.LogDebug(_connectTitle);

                return true;
            }

            _logger.LogCritical(
                "FATAL ERROR: Connection to the {VirtualHost} virtual host of {HostName} RabbitMQ host could not be created and opened",
                _connectionOptions.VirtualHost, _connectionOptions.HostName);
            return false;
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

    void OnConnectionBlocked(object sender, ConnectionBlockedEventArgs e)
    {
        if (_disposed) return;

        TryConnect();
    }

    void OnCallbackException(object sender, CallbackExceptionEventArgs e)
    {
        if (_disposed) return;

        TryConnect();
    }

    void OnConnectionShutdown(object sender, ShutdownEventArgs reason)
    {
        if (_disposed) return;

        TryConnect();
    }

    public IModel CreateChannel()
    {
        TryConnect();

        if (!IsConnected)
            throw new InvalidOperationException(
                $"RabbitMQ connection is not opened yet to the {_connectionOptions.VirtualHost} virtual host of {_connectionOptions.HostName}.");

        return _connection.CreateModel();
    }

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
            _connection?.Dispose();
            _connection = null;

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