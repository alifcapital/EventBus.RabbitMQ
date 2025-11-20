using EventBus.RabbitMQ.Subscribers.Models;

namespace OrdersService.Messaging.Events.Subscribers;

public record OrderFailed : SubscribeEvent
{
    public Guid OrderId { get; init; }
    
    public string Reason { get; init; }
}