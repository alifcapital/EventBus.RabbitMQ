using EventBus.RabbitMQ.Models;
using EventBus.RabbitMQ.Subscribers.Models;

namespace OrdersService.Messaging.Events.Subscribers;

public record PaymentProcessed : SubscribeEvent, ISaga
{
    public Guid OrderId { get; init; }
    
    public Guid CorrelationId { get; init; }
}