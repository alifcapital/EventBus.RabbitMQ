using EventStorage.Models;

namespace EventBus.RabbitMQ.Information.Models;

/// <summary>
/// The settings structure of subscribed event including its all handlers information.
/// The subscribers are grouped by the event type name, and a received message is passed to every handler of that name,
/// so the handlers may handle event classes of different assemblies (for example, of different modules).
/// </summary>
public record EventSubscriberInformation
{
    /// <summary>
    /// The name of the event type which is attached to a received message and is used for finding its handlers.
    /// </summary>
    public required string EventTypeName { get; init; }

    /// <summary>
    /// The routing key which is used for binding the queue to the exchange.
    /// </summary>
    public required string RoutingKey { get; init; }

    /// <summary>
    /// The name of the queue from which the event is received.
    /// </summary>
    public required string QueueName { get; init; }

    /// <summary>
    /// The key of the entry in the <c>VirtualHostSettings</c> section of the configuration which is used by the event,
    /// or null if the default settings are used.
    /// </summary>
    public string VirtualHostKey { get; init; }

    /// <summary>
    /// The naming policy for deserializing the properties of the event.
    /// </summary>
    public NamingPolicyType? PropertyNamingPolicy { get; init; }

    /// <summary>
    /// The resolved settings of the virtual host which is used by the event, or null if they are not resolved yet.
    /// </summary>
    public required RabbitMqVirtualHostInformation VirtualHostSettings { get; init; }

    /// <summary>
    /// All handlers of the event with the information of the event classes they handle.
    /// </summary>
    public required IReadOnlyList<EventHandlerInformation> Handlers { get; init; }
}
