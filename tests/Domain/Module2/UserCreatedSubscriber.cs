using EventBus.RabbitMQ.Subscribers.Models;

namespace EventBus.RabbitMQ.Tests.Domain.Module2;

public class UserCreatedSubscriber : IEventSubscriber<UserCreated>
{
    public Task Receive(UserCreated @event)
    {
        return Task.CompletedTask;
    }
}