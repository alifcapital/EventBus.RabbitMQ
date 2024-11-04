using EventBus.RabbitMQ.Models;
using EventStorage.Inbox.Models;

namespace EventBus.RabbitMQ.Subscribers.Models;

/// <summary>
/// Base interface for all subscriber classes
/// </summary>
public interface ISubscribeEvent : IBaseEvent, IReceiveEvent
{
    /// <summary>
    /// The id of event
    /// </summary>
    public new Guid EventId { get; set; }
}