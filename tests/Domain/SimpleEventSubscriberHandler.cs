using EventBus.RabbitMQ.Subscribers.Models;

namespace EventBus.RabbitMQ.Tests.Domain;

public class SimpleEventSubscriberHandler: IEventSubscriber<SimpleSubscribeEvent>
{
    public Task Receive(SimpleSubscribeEvent @event)
    {
        return Task.CompletedTask;
    }
}