using EventBus.RabbitMQ.Publishers.Models;

namespace EventBus.RabbitMQ.Publishers.Managers;

/// <summary>
/// This interface will provide the ability to publish an event to the event bus.
/// </summary>
public interface IEventPublisherManager : IDisposable
{
    /// <summary>
    /// Publishing an event to event bus immediately.
    /// </summary>
    /// <param name="publishEvent">Event to publish</param>
    /// <typeparam name="TPublishEvent">Event type that must implement from the IPublishEvent</typeparam>
    public Task PublishAsync<TPublishEvent>(TPublishEvent publishEvent)
        where TPublishEvent : class, IPublishEvent;
    
    /// <summary>
    /// The first to collect all publishing events to the memory and then publish them to the RabbitMQ while finishing the scope/request of API.
    /// </summary>
    /// <param name="publishEvent">Event to collect</param>
    /// <typeparam name="TPublishEvent">Event type that must implement from the IPublishEvent</typeparam>
    public void Collect<TPublishEvent>(TPublishEvent publishEvent)
        where TPublishEvent : class, IPublishEvent;
    
    /// <summary>
    /// Clean all collected events from the memory.
    /// </summary>
    public void CleanCollectedEvents();
}