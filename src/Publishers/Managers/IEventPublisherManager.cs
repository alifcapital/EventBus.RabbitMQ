using EventBus.RabbitMQ.Publishers.Models;

namespace EventBus.RabbitMQ.Publishers.Managers;

/// <summary>
/// This interface will provide the ability to publish an event to the event bus.
/// </summary>
public interface IEventPublisherManager
{
    /// <summary>
    /// Publishing an event to event bus.
    /// </summary>
    /// <param name="publishEvent">Event to publish</param>
    /// <typeparam name="TPublishEvent">Event type that must implement from the IPublishEvent</typeparam>
    public Task PublishAsync<TPublishEvent>(TPublishEvent publishEvent)
        where TPublishEvent : class, IPublishEvent;
}