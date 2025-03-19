using EventBus.RabbitMQ.Publishers.Models;

namespace EventBus.RabbitMQ.Publishers.Managers;

public interface IEventPublisherManager
{
    /// <summary>
    /// Publishing an event to event bus
    /// </summary>
    /// <param name="event">Event to publish</param>
    /// <typeparam name="TEvent">Event type that must implement from the IPublishEvent</typeparam>
    public Task PublishAsync<TEvent>(TEvent @event) where TEvent : IPublishEvent;

    /// <summary>
    /// Creating an exchange for each registered publisher and 
    /// </summary>
    internal void CreateExchangeForPublishers();
}