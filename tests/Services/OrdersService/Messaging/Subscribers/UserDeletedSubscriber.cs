using EventBus.RabbitMQ.Subscribers.Models;
using OrdersService.Messaging.Events;
using OrdersService.Messaging.Events.Subscribers;

namespace OrdersService.Messaging.Subscribers;

public class UserDeletedSubscriber : IEventSubscriber<UserDeleted>
{
    public async Task HandleAsync(UserDeleted @event)
    {
        await Task.CompletedTask;
    }
}