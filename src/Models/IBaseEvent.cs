using EventStorage.Models;

namespace EventBus.RabbitMQ.Models;

/// <summary>
/// Base interface for all publisher and subscriber interface
/// </summary>
public interface IBaseEvent : IEvent, IHasHeaders
{
    /// <summary>
    /// Created time of event
    /// </summary>
    public DateTime CreatedAt { get; init; }
}