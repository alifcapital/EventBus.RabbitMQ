using EventBus.RabbitMQ.Publishers.Models;

namespace UsersService.Messaging.Events.Publishers;

public record PaymentProcessed : PublishEvent
{
    public Guid OrderId { get; init; }
}