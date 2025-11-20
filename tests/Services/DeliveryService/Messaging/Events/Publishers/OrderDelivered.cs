using EventBus.RabbitMQ.Publishers.Models;

namespace DeliveryService.Messaging.Events.Publishers;

public record OrderDelivered : PublishEvent
{
    public Guid OrderId { get; init; }
}