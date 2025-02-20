using EventBus.RabbitMQ.Publishers.Models;

namespace UsersService.Messaging.Events.Publishers;

public record UserDeleted : PublishEvent
{
    public Guid UserId { get; init; }
    
    public string UserName { get; init; }
}