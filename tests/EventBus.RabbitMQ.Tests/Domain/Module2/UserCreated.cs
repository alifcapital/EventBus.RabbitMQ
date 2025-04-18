using EventBus.RabbitMQ.Subscribers.Models;

namespace EventBus.RabbitMQ.Tests.Domain.Module2;

public record UserCreated : SubscribeEvent
{
    public string Type { get; init; }
    public DateTime Date { get; init; }
}