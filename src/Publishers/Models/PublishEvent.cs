using System.Text.Json.Serialization;

namespace EventBus.RabbitMQ.Publishers.Models;

/// <summary>
/// The base class for all publisher classes.
/// </summary>
public abstract record PublishEvent : IPublishEvent
{
    protected PublishEvent()
    {
        EventId = Guid.CreateVersion7();
        CreatedAt = DateTime.Now;
    }

    public Guid EventId { get; init; }

    public DateTime CreatedAt { get; init; }

    [JsonIgnore]
    public Dictionary<string, string> Headers { get; set; }
}