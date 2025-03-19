using EventBus.RabbitMQ.Publishers.Models;

namespace EventBus.RabbitMQ.Publishers.Managers;

public interface IEventPublisherManager
{
    /// <summary>
    /// Publishing an event to event bus
    /// </summary>
    /// <param name="event">Event to publish</param>
    /// <typeparam name="TEventPublisher">Event type that must implement from the IEventPublisher</typeparam>
    public void Publish<TEventPublisher>(TEventPublisher @event) where TEventPublisher : IPublishEvent;

    /// <summary>
    /// Creating an exchange for each registered publisher and 
    /// </summary>
    internal void CreateExchangeForPublishers();
}