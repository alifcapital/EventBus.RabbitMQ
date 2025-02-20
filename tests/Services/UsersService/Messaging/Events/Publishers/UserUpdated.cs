using EventBus.RabbitMQ.Publishers.Models;

namespace UsersService.Messaging.Events.Publishers;

public record UserUpdated : PublishEvent
{
    public Guid UserId { get; init; }
    
    public string OldUserName { get; init; }
    
    public string NewUserName { get; init; }
}