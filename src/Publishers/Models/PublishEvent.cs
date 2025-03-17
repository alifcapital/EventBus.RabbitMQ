namespace EventBus.RabbitMQ.Publishers.Models;

/// <summary>
/// The base class for all publisher classes.
/// </summary>
public abstract record PublishEvent : IPublishEvent
{
    public Guid EventId { get; init; } = Guid.CreateVersion7();

    public DateTime CreatedAt { get; init; } = DateTime.Now;

    public Dictionary<string, string> Headers { get; set; }
}