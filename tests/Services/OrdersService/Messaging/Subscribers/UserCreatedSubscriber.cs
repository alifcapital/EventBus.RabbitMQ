using EventBus.RabbitMQ.Subscribers.Models;
using OrdersService.Messaging.Events;
using OrdersService.Messaging.Events.Module1;

namespace OrdersService.Messaging.Subscribers;

public class UserCreatedSubscriber(ILogger<UserCreatedSubscriber> logger) : IEventSubscriber<UserCreated>
{
    public async Task HandleAsync(UserCreated @event)
    {
        logger.LogInformation("Id ({Id}): '{UserName}' user is created with the {UserId} id", @event.EventId,
            @event.UserName, @event.UserId);

        await Task.CompletedTask;
    }
}

public class UserCreatedSubscriber2 : IEventSubscriber<UserCreated>
{
    public async Task HandleAsync(UserCreated @event)
    {
        await Task.CompletedTask;
    }
}

public class UserCreatedSubscriber3 : IEventSubscriber<Events.Module2.UserCreated>
{
    public async Task HandleAsync(Events.Module2.UserCreated @event)
    {
        await Task.CompletedTask;
    }
}