using System.Text.Json.Serialization;
using EventBus.RabbitMQ.Publishers.Models;
using EventStorage.Outbox.Models;

namespace UsersService.Messaging.Events.Publishers;

public record UserCreated : PublishEvent, IHasAdditionalData
{
    public Guid UserId { get; init; }
    
    public string UserName { get; init; }
    
    [JsonIgnore]
    public Dictionary<string, string> AdditionalData { get; set; }
}