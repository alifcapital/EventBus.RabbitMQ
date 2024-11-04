using EventBus.RabbitMQ.Subscribers.Options;

namespace EventBus.RabbitMQ.Subscribers.Consumers;

internal interface IEventConsumerServiceCreator
{
    public IEventConsumerService Create(EventSubscriberOptions connectionOptions, IServiceProvider serviceProvider, bool useInbox);
}