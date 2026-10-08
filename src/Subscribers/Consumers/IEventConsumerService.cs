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
    public Task CreateChannelAndSubscribeReceiverAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stops receiving new events, waits for the events being handled and closes the consumer channel.
    /// </summary>
    /// <param name="cancellationToken">The token to stop waiting for the events being handled.</param>
    public Task StopReceivingEventsAsync(CancellationToken cancellationToken);
}
