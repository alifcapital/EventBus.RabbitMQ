using EventBus.RabbitMQ.Subscribers.Models;

namespace EventBus.RabbitMQ.Tests.Domain;

public record SimpleSubscribeEvent: SubscribeEvent
{
    public string Name { get; set; }
    public string Type { get; set; }
    public bool Enabled { get; set; }
    public Dictionary<string, string[]> Attributes { get; set; }
}