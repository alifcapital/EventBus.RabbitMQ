using EventBus.RabbitMQ.Subscribers.Options;

namespace EventBus.RabbitMQ.Subscribers.Consumers;

internal class EventConsumerServiceCreator: IEventConsumerServiceCreator
{
    public IEventConsumerService Create(EventSubscriberOptions connectionOptions, IServiceProvider serviceProvider, bool useInbox)
    {
        return new EventConsumerService(connectionOptions, serviceProvider, useInbox);
    }
}