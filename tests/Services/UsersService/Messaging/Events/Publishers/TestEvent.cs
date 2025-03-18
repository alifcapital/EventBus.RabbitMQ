using EventStorage.Outbox.Models;

namespace UsersService.Messaging.Events.Publishers;

public record TestEvent : IOutboxEvent
{
    public Guid EventId { get; init; }
}