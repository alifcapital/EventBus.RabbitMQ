using EventBus.RabbitMQ.Subscribers.Models;
using EventBus.RabbitMQ.Subscribers.Options;

namespace EventBus.RabbitMQ.Subscribers.Managers;

/// <summary>
/// This interface is responsible for collecting all the subscriber information and create a consumer for each queue.
/// </summary>
internal interface IEventSubscriberCollector
{
    /// <summary>
    /// Registers a subscriber 
    /// </summary>
    /// <param name="options">The options specific to the subscriber, if any.</param>
    /// <typeparam name="TEvent">Event which we want to subscribe</typeparam>
    /// <typeparam name="TEventHandler">Handler class of the event which we want to receive event</typeparam>
    public void AddSubscriber<TEvent, TEventHandler>(Action<EventSubscriberOptions> options = null)
        where TEvent : class, ISubscribeEvent
        where TEventHandler : class, IEventSubscriber<TEvent>;

    /// <summary>
    /// Creating and register each unique a queue for different virtual host and start receiving events
    /// </summary>
    public void CreateConsumerForEachQueueAndStartReceivingEvents();

    /// <summary>
    /// For printing all loaded subscribers information to the logger for investigation purposes.
    /// </summary>
    internal void PrintLoadedSubscribersInformation();
}