using EventBus.RabbitMQ.Subscribers.Options;

namespace EventBus.RabbitMQ.Subscribers.Consumers;

internal interface IEventConsumerService
{
    /// <summary>
    /// Registers a subscriber 
    /// </summary>
    /// <param name="eventInfo">Event and handler types with the settings which we want to subscribe</param>
    public void AddSubscriber((Type eventType, Type eventHandlerType, EventSubscriberOptions eventSettings) eventInfo);

    /// <summary>
    /// Starts receiving events by creating a consumer
    /// </summary>
    public void StartAndSubscribeReceiver();
}