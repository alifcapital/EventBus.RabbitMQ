using EventBus.RabbitMQ.Subscribers.Models;

namespace OrdersService.Messaging.Events.Subscribers;

public record OrderDelivered : SubscribeEvent
{
    public Guid OrderId { get; init; }
}