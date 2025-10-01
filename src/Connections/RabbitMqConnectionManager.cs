using EventBus.RabbitMQ.Configurations;

namespace EventBus.RabbitMQ.Connections;

internal class RabbitMqConnectionManager(IServiceProvider serviceProvider) : IRabbitMqConnectionManager
{
    private readonly Dictionary<string, RabbitMqConnection> _connections = [];

    public IRabbitMqConnection GetOrCreateConnection(RabbitMqHostSettings virtualHostSettings)
    {
        var virtualHostKey = GetVirtualHostUniqueKey(virtualHostSettings);
        if (_connections.TryGetValue(virtualHostKey, out var connection))
            return connection;

        connection = new RabbitMqConnection(virtualHostSettings, serviceProvider);
        _connections[virtualHostKey] = connection;

        return connection;
    }

    #region Helper methods

    /// <summary>
    /// Get a unique key for the virtual host settings to identify connections.
    /// </summary>
    private string GetVirtualHostUniqueKey(RabbitMqHostSettings virtualHostSettings)
    {
        return $"{virtualHostSettings.HostName}:{virtualHostSettings.HostPort}:{virtualHostSettings.VirtualHost}";
    }

    #endregion
}