using EventBus.RabbitMQ.Subscribers.Models;

namespace UsersService.Messaging.Events.Subscribers;

public record PaymentCreated : SubscribeEvent
{
    public Guid PaymentId { get; init; }

    public Guid UserId { get; init; }

    public double Amount { get; set; }
}