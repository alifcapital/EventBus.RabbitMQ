using EventBus.RabbitMQ.Publishers.Options;

namespace EventBus.RabbitMQ.Publishers.Models;

internal record PublisherInformation
{
    /// <summary>
    /// The type of the publishing event.
    /// </summary>
    public required Type EventType { get; init; }

    /// <summary>
    /// The settings of the event publisher.
    /// </summary>
    public required EventPublisherOptions Settings { get; set; }

    /// <summary>
    /// The type of the specific outbox publisher (<c>IMessageBrokerEventPublisher&lt;TEvent&gt;</c>) of the event, or null if
    /// the event does not have it and the global publisher is used.
    /// </summary>
    public Type MessageBrokerEventPublisherType { get; set; }
}
