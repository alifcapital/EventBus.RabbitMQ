using EventBus.RabbitMQ.Subscribers.Models;

namespace DeliveryService.Messaging.Events.Subscribers;

public record DeliverOrder : SubscribeEvent
{
    public Guid OrderId { get; init; }

    public string ProductName { get; set; }

    public int Quantity { get; set; }
}