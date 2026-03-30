using EventBus.RabbitMQ.Publishers.Models;

namespace EventBus.RabbitMQ.Publishers.Managers;

/// <summary>
/// This interface will provide the ability to publish an event to the event bus.
/// </summary>
public interface IEventPublisherManager : IDisposable
{
    /// <summary>
    /// Publishing an event to event bus. If the Outbox functionality is enabled,
    /// it will store the event to the outbox instead of publishing it directly to the RabbitMQ.
    /// Otherwise, it will publish the event immediately to the RabbitMQ.
    /// </summary>
    /// <param name="publishEvent">Event to publish</param>
    /// <param name="cancellationToken">Cancellation token to cancel the publishing process</param>
    /// <typeparam name="TPublishEvent">Event type that must implement from the IPublishEvent</typeparam>
    public Task PublishAsync<TPublishEvent>(TPublishEvent publishEvent, CancellationToken cancellationToken)
        where TPublishEvent : class, IPublishEvent;
    
    /// <summary>
    /// The first to collect all publishing events to the memory and then publish them while finishing the scope/request of API.
    /// If the Outbox functionality is enabled, it will store the event to the outbox first, then publish.
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