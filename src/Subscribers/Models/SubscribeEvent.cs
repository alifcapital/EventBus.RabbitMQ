using System.Text.Json.Serialization;
using EventStorage.Models;

namespace EventBus.RabbitMQ.Subscribers.Models;

/// <summary>
/// The base class for all subscribe classes.
/// </summary>
public abstract record SubscribeEvent : ISubscribeEvent
{
    public Guid EventId { get; set; }
    
    Guid IEvent.EventId
    {
        get => EventId;
        init => EventId = value;
    }

    public DateTime CreatedAt { get; init; }

    [JsonIgnore]
    public Dictionary<string, string> Headers { get; set; }
}