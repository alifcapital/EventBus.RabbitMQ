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
}
