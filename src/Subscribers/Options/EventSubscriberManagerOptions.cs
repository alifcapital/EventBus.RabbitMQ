using EventBus.RabbitMQ.Subscribers.Managers;
using EventBus.RabbitMQ.Subscribers.Models;

namespace EventBus.RabbitMQ.Subscribers.Options;

public class EventSubscriberManagerOptions
{
    private readonly EventSubscriberManager _subscriberManager;

    internal EventSubscriberManagerOptions(EventSubscriberManager subscriberManager)
    {
        _subscriberManager = subscriberManager;
    }

    /// <summary>
    /// Registers a subscriber 
    /// </summary>
    /// <param name="options">The options specific to the subscriber, if any.</param>
    /// <typeparam name="TEvent">Event which we want to subscribe</typeparam>
    /// <typeparam name="TEventHandler">Handler class of the event which we want to receive event</typeparam>
    public void AddSubscriber<TEvent, TEventHandler>(Action<EventSubscriberOptions> options = null)
        where TEvent : class, ISubscribeEvent
        where TEventHandler : class, IEventSubscriber<TEvent>
    {
        _subscriberManager.AddSubscriber<TEvent, TEventHandler>(options);
    }
}