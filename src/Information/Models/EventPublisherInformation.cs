using EventStorage.Models;

namespace EventBus.RabbitMQ.Information.Models;

/// <summary>
/// The settings structure of publishing event including publisher information.
/// </summary>
public record EventPublisherInformation
{
    /// <summary>
    /// The information of the class of the publishing event.
    /// </summary>
    public required EventTypeInformation EventType { get; init; }

    /// <summary>
    /// The name of the event type which is attached to the published message. It may differ from the class name because
    /// of the event naming policy or a custom name from the configuration.
    /// </summary>
    public required string EventTypeName { get; init; }

    /// <summary>
    /// The routing key which is used while publishing the event.
    /// </summary>
    public required string RoutingKey { get; init; }

    /// <summary>
    /// The key of the entry in the <c>VirtualHostSettings</c> section of the configuration which is used by the event,
    /// or null if the default settings are used.
    /// </summary>
    public string VirtualHostKey { get; init; }

    /// <summary>
    /// The naming policy for serializing the properties of the event.
    /// </summary>
    public NamingPolicyType? PropertyNamingPolicy { get; init; }

    /// <summary>
    /// The resolved settings of the virtual host which is used by the event, or null if they are not resolved yet.
    /// </summary>
    public required RabbitMqVirtualHostInformation VirtualHostSettings { get; init; }
}
