using EventBus.RabbitMQ.Subscribers.Models;

namespace EventBus.RabbitMQ.Tests.Domain.Module2;

public class UserCreatedSubscriber : IEventSubscriber<UserCreated>
{
    public async Task HandleAsync(UserCreated @event)
    {
        await Task.CompletedTask;
    }
}