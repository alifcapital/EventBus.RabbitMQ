using EventBus.RabbitMQ.Publishers.Models;

namespace OrdersService.Messaging.Events.Publishers;

public record DeliverOrder : PublishEvent
{
    public Guid OrderId { get; init; }

    public string ProductName { get; set; }

    public int Quantity { get; set; }
}