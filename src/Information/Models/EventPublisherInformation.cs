using EventStorage.Models;

namespace EventBus.RabbitMQ.Information.Models;

/// <summary>
/// The settings structure of publishing event including publisher information.
/// </summary>
public record EventPublisherInformation
{
    /// <summary>
    /// The information of the publishing event and of its handler, which publishes it: the specific outbox publisher
    /// (<c>IMessageBrokerEventPublisher&lt;TEvent&gt;</c>) of the event or the global <c>MessageBrokerEventPublisher</c>
    /// when the outbox is used, otherwise the <c>EventPublisherManager</c> which publishes it to the RabbitMQ directly.
    /// </summary>
    public required EventHandlerInformation Handler { get; init; }

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
