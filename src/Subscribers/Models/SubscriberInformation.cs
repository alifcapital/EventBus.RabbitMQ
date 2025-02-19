namespace EventBus.RabbitMQ.Subscribers.Models;

internal record SubscriberInformation
{
    /// <summary>
    /// The type of the received event.
    /// </summary>
    public required Type EventType { get; init; }
    
    /// <summary>
    /// The type of the event subscriber.
    /// </summary>
    public required Type EventSubscriberType { get; init; }
}