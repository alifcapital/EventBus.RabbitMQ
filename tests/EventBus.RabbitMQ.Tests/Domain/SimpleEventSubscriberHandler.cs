using EventBus.RabbitMQ.Subscribers.Models;

namespace EventBus.RabbitMQ.Tests.Domain;

public class SimpleEventSubscriberHandler : IEventSubscriber<SimpleSubscribeEvent>
{
    public async Task HandleAsync(SimpleSubscribeEvent @event)
    {
        await Task.CompletedTask;
    }
}