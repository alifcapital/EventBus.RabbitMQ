using EventBus.RabbitMQ.Configurations;

namespace EventBus.RabbitMQ.Connections;

/// <summary>
/// Creates and manages RabbitMQ connections
/// </summary>
internal interface IRabbitMqConnectionManager
{
    /// <summary>
    /// Gets or creates a RabbitMQ connection based on the provided event settings.
    /// </summary>
    /// <param name="virtualHostSettings">The RabbitMQ's virtual host settings to create or get the connection</param>
    /// <returns>Newly created if there is no existing connection with the same unique key ($"{HostName}:{HostPort}:{VirtualHost}"), otherwise returns the existing one</returns>
    public IRabbitMqConnection GetOrCreateConnection(RabbitMqHostSettings virtualHostSettings);
}