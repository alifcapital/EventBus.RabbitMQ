using EventStorage.Outbox.Managers;
using InMemoryMessaging.Models;
using Microsoft.EntityFrameworkCore;
using OrdersService.Infrastructure;
using OrdersService.Messaging.Events.Publishers;
using OrdersService.Models;

namespace OrdersService.Messaging.InMemoryMessageHandlers;

public class OrderSubmittedHandler(IOutboxEventManager outboxEventManager, OrderContext context)
    : IMessageHandler<OrderSubmitted>
{
    public async Task HandleAsync(OrderSubmitted message)
    {
        var orderCreatedEvent = new ProcessPayment
        {
            OrderId = message.OrderId,
            Amount = message.TotalPrice,
            CustomerEmail = message.CustomerEmail
        };
        var order = context.Orders.AsTracking().Single(u => u.Id == message.OrderId);
        order.Status = OrderStatus.ProcessingPayment;
        await context.SaveChangesAsync();

        await outboxEventManager.StoreAsync(orderCreatedEvent);
    }
}