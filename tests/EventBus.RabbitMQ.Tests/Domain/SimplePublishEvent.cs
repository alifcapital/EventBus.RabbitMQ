using EventBus.RabbitMQ.Publishers.Models;

namespace EventBus.RabbitMQ.Tests.Domain;

public record SimplePublishEvent : PublishEvent
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Type { get; set; }
    public bool Enabled { get; set; }
    public Dictionary<string, string[]> Attributes { get; set; }
}