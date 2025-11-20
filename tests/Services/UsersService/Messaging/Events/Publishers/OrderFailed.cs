using EventBus.RabbitMQ.Publishers.Models;

namespace UsersService.Messaging.Events.Publishers;

public record OrderFailed : PublishEvent
{
    public Guid OrderId { get; init; }
    
    public string Reason { get; init; }
}