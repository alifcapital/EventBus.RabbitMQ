using EventBus.RabbitMQ.Subscribers.Models;
using EventBus.RabbitMQ.Subscribers.Options;

namespace EventBus.RabbitMQ.Subscribers.Consumers;

internal interface IEventConsumerService
{
    /// <summary>
    /// Registers a subscriber 
    /// </summary>
    /// <param name="eventInfo">Event and handler types with the settings which we want to subscribe</param>
    public void AddSubscriber(SubscribersInformation eventInfo);

    /// <summary>
    /// Starts receiving events by creating a consumer
    /// </summary>
    public void CreateChannelAndSubscribeReceiver();
    
    /// <summary>
    /// Gets the settings of the event subscriber which is used to create the consumer
    /// </summary>
    public EventSubscriberOptions GetEventSubscriberSettings();
}