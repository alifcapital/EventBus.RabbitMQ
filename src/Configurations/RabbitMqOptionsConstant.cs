using System.Security.Authentication;
using EventStorage.Models;

namespace EventBus.RabbitMQ.Configurations;

internal static class RabbitMqOptionsConstant
{
    /// <summary>
    /// The host name of the RabbitMQ server. Default value is "localhost".
    /// </summary>
    const string HostName = "localhost";

    /// <summary>
    /// The port number on which RabbitMQ is running. Default value is "5672".
    /// </summary>
    const int HostPort = 5672;

    /// <summary>
    /// The virtual host of the RabbitMQ server. Default value is "/".
    /// </summary>
    const string VirtualHost = "/";

    /// <summary>
    /// The username to connect to RabbitMQ. Default value is "guest".
    /// </summary>
    const string UserName = "guest";

    /// <summary>
    /// The password for the specified username to connect to RabbitMQ. Default value is "guest".
    /// </summary>
    const string Password = "guest";

    /// <summary>
    /// The name of the exchange to use in RabbitMQ. Default value is "DefaultExchange".
    /// </summary>
    const string ExchangeName = "DefaultExchange";

    /// <summary>
    /// The type of the exchange to use in RabbitMQ. Default value is "topic". It can be one of "direct", "fanout", or "topic".
    /// </summary>
    const string ExchangeType = "topic";

    /// <summary>
    /// The name of the queue to use in RabbitMQ.
    /// </summary>
    public const string QueueName = "DefaultQueue";

    /// <summary>
    /// Retry count to connect to the RabbitMQ. Default value is "3".
    /// </summary>
    const int RetryConnectionCount = 3;

    /// <summary>
    /// Indicates whether TLS/SSL should be used for the connection.
    /// When set to true, the connection will be secured using TLS/SSL.
    /// </summary>
    const bool UseTls = false;
        
    /// <summary>
    /// Retrieve or set the TLS protocol version. The default value is "Tls".
    /// </summary>
    /// <seealso cref="T:System.Security.Authentication.SslProtocols" />
#pragma warning disable SYSLIB0039
    const SslProtocols SslProtocolVersion = SslProtocols.Tls;
#pragma warning restore SYSLIB0039    

    /// <summary>
    /// Create a default RabbitMQ virtual host settings
    /// </summary>
    /// <returns>Returns a new virtual host settings</returns>
    public static RabbitMqOptions CreateDefaultRabbitMqOptions()
    {
        return new RabbitMqOptions
        {
            HostName = HostName,
            HostPort = HostPort,
            VirtualHost = VirtualHost,
            UserName = UserName,
            Password = Password,
            ExchangeName = ExchangeName,
            ExchangeType = ExchangeType,
            QueueName = QueueName,
            RetryConnectionCount = RetryConnectionCount,
            PropertyNamingPolicy = NamingPolicyType.PascalCase,
            EventNamingPolicy = NamingPolicyType.PascalCase,
            UseTls = UseTls,
            SslProtocolVersion = SslProtocolVersion
        };
    }
}