using EventBus.RabbitMQ.Models;
using EventStorage.Outbox.Models;

namespace EventBus.RabbitMQ.Publishers.Models;

/// <summary>
/// The base interface for all publish classes.
/// </summary>
public interface IPublishEvent : IBaseEvent, IOutboxEvent;