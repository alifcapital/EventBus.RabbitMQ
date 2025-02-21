using EventBus.RabbitMQ.Subscribers.Models;

namespace OrdersService.Messaging.Subscribers;

public class UserCreatedSubscriber1(ILogger<UserCreatedSubscriber1> logger) : IEventSubscriber<Events.Module1.UserCreated>
{
    public async Task HandleAsync(Events.Module1.UserCreated @event)
    {
        logger.LogInformation("Id ({Id}): '{UserName}' user is created with the {UserId} id", @event.EventId,
            @event.UserName, @event.UserId);
        
        await Task.CompletedTask;
    }
}

public class UserCreatedSubscriber2(ILogger<UserCreatedSubscriber2> logger) : IEventSubscriber<Events.Module1.UserCreated>
{
    public async Task HandleAsync(Events.Module1.UserCreated @event)
    {
        logger.LogInformation("Id ({Id}): '{UserName}' user is created with the {UserId} id", @event.EventId,
            @event.UserName, @event.UserId);
        
        await Task.CompletedTask;
    }
}

public class UserCreatedSubscriber3(ILogger<UserCreatedSubscriber3> logger) : IEventSubscriber<Events.Module2.UserCreated>
{
    public async Task HandleAsync(Events.Module2.UserCreated @event)
    {
        logger.LogInformation("Id ({Id}): '{UserName}' user is created with the {UserId} id", @event.EventId,
            @event.UserName, @event.UserId);
        
        await Task.CompletedTask;
    }
}