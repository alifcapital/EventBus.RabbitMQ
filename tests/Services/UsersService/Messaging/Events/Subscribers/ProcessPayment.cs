using EventBus.RabbitMQ.Subscribers.Models;

namespace UsersService.Messaging.Events.Subscribers;

public record ProcessPayment : SubscribeEvent
{
    public Guid OrderId { get; init; }
    public string CustomerEmail { get; init; }
    public decimal Amount { get; init; }
}