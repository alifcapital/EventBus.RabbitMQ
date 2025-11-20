using EventBus.RabbitMQ.Publishers.Models;

namespace OrdersService.Messaging.Events.Publishers;

public record RefundPayment : PublishEvent
{
    public Guid OrderId { get; init; }
    public string CustomerEmail { get; init; }
    public decimal Amount { get; init; }
}