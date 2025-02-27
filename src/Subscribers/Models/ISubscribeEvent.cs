using EventBus.RabbitMQ.Models;
using EventStorage.Inbox.Models;

namespace EventBus.RabbitMQ.Subscribers.Models;

/// <summary>
/// The base interface for all subscriber classes.
/// </summary>
public interface ISubscribeEvent : IBaseEvent, IInboxEvent
{
    /// <summary>
    /// The id of event
    /// </summary>
    public new Guid EventId { get; set; }
}