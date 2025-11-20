using InMemoryMessaging.Models;

namespace OrdersService.Messaging.Events.Publishers;

public record OrderSubmitted : IMessage
{
    public Guid OrderId { get; init; }
    public decimal TotalPrice { get; init; }
    public string CustomerEmail { get; init; }
}