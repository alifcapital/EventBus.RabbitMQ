using EventBus.RabbitMQ.Models;
using EventStorage.Outbox.Models;

namespace EventBus.RabbitMQ.Publishers.Models;

/// <summary>
/// Base interface for all publish classes
/// </summary>
public interface IPublishEvent : IBaseEvent, ISendEvent
{
}