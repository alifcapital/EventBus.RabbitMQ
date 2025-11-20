using EventBus.RabbitMQ.Subscribers.Models;

namespace OrdersService.Messaging.Events.Subscribers;

public record UserDeleted : SubscribeEvent
{
    public Guid UserId { get; init; }
    
    public string UserName { get; init; }
}