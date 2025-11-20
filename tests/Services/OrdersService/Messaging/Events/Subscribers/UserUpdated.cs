using EventBus.RabbitMQ.Subscribers.Models;

namespace OrdersService.Messaging.Events.Subscribers;

public record UserUpdated : SubscribeEvent
{
    public Guid UserId { get; init; }
    
    public string OldUserName { get; init; }
    
    public string NewUserName { get; init; }
}