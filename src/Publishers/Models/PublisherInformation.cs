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
    /// The type of the publisher of the event: its specific outbox publisher (<c>IMessageBrokerEventPublisher&lt;TEvent&gt;</c>)
    /// or the global <c>MessageBrokerEventPublisher</c> if the event does not have a specific one.
    /// </summary>
    public required Type PublisherType { get; init; }
}
