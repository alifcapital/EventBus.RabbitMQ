using EventBus.RabbitMQ.Configurations;
using EventStorage.Models;

namespace EventBus.RabbitMQ.Information.Models;

/// <summary>
/// The resolved settings of the virtual host which is used by a loaded publisher or subscriber.
/// It intentionally does not contain the connection secrets and the address of the server (host name, port, user name,
/// password and paths of the client certificate), so it is safe to show it to a user.
/// </summary>
public record LoadedVirtualHostInformation
{
    /// <summary>
    /// The name of the virtual host on the RabbitMQ server.
    /// </summary>
    public required string VirtualHost { get; init; }

    /// <summary>
    /// The name of the exchange which is used by the event.
    /// </summary>
    public required string ExchangeName { get; init; }

    /// <summary>
    /// The type of the exchange: "direct", "fanout" or "topic".
    /// </summary>
    public required string ExchangeType { get; init; }

    /// <summary>
    /// The naming policy for the event type name.
    /// </summary>
    public NamingPolicyType? EventNamingPolicy { get; init; }

    /// <summary>
    /// Indicates whether the broker must confirm each published event.
    /// </summary>
    public bool? PublisherConfirmation { get; init; }

    /// <summary>
    /// Indicates whether TLS/SSL is used for the connection.
    /// </summary>
    public bool? UseTls { get; init; }

    /// <summary>
    /// The arguments which are used while declaring a queue.
    /// </summary>
    public IReadOnlyDictionary<string, object> QueueArguments { get; init; } = new Dictionary<string, object>();

    /// <summary>
    /// The arguments which are used while declaring an exchange.
    /// </summary>
    public IReadOnlyDictionary<string, object> ExchangeArguments { get; init; } = new Dictionary<string, object>();

    /// <summary>
    /// Creates the information from the resolved settings of the virtual host by skipping the connection secrets.
    /// </summary>
    /// <param name="settings">The resolved settings of the virtual host.</param>
    /// <returns>The information of the virtual host, or null if the settings are not resolved yet.</returns>
    internal static LoadedVirtualHostInformation Create(RabbitMqHostSettings settings)
    {
        if (settings is null)
            return null;

        return new LoadedVirtualHostInformation
        {
            VirtualHost = settings.VirtualHost,
            ExchangeName = settings.ExchangeName,
            ExchangeType = settings.ExchangeType,
            EventNamingPolicy = settings.EventNamingPolicy,
            PublisherConfirmation = settings.PublisherConfirmation,
            UseTls = settings.UseTls,
            QueueArguments = new Dictionary<string, object>(settings.QueueArguments ?? []),
            ExchangeArguments = new Dictionary<string, object>(settings.ExchangeArguments ?? [])
        };
    }
}
