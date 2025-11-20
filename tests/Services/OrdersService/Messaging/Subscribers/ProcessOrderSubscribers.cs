using EventBus.RabbitMQ.Subscribers.Models;
using EventStorage.Outbox.Managers;
using InMemoryMessaging.Models;
using Microsoft.EntityFrameworkCore;
using OrdersService.Infrastructure;
using OrdersService.Messaging.Events.Publishers;
using OrdersService.Messaging.Events.Subscribers;
using OrdersService.Models;

namespace OrdersService.Messaging.Subscribers;

public class PaymentProcessedSubscribers(
    OrderContext context,
    IOutboxEventManager outboxEventManager) :
    IMessageHandler<OrderSubmitted>,
    IEventSubscriber<PaymentProcessed>,
    IEventSubscriber<OrderDelivered>,
    IEventSubscriber<OrderFailed>
{
    // private Order order;
    // public void Init(ISaga @event)
    // {
    //     order = context.Orders.AsTracking().Single(u => u.Id == @event.CorrelationId);
    // }
    
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

    public async Task HandleAsync(PaymentProcessed @event)
    {
        var order = context.Orders.AsTracking().Single(u => u.Id == @event.OrderId);
        order.Status = OrderStatus.Delivering;

        var deliverOrder = new DeliverOrder
        {
            OrderId = order.Id,
            ProductName = order.ProductName,
            Quantity = order.Quantity
        };
        outboxEventManager.Collect(deliverOrder);
        await context.SaveChangesAsync();
    }
    
    public async Task HandleAsync(OrderDelivered @event)
    {
        var order = context.Orders.AsTracking().Single(u => u.Id == @event.OrderId);
        order.Status = OrderStatus.Completed;
        await context.SaveChangesAsync();
    }

    public async Task HandleAsync(OrderFailed @event)
    {
        var order = context.Orders.AsTracking().Single(u => u.Id == @event.OrderId);
        if (order.Status == OrderStatus.Delivering)
        {
            order.Status = OrderStatus.Cancelled;

            var refundPayment = new RefundPayment
            {
                OrderId = order.Id,
                Amount = order.TotalPrice,
                CustomerEmail = order.CustomerEmail
            };
            outboxEventManager.Collect(refundPayment);
        }
        else
        {
            order.Status = OrderStatus.Cancelled;
        }

        await context.SaveChangesAsync();
    }

    // public async Task DownAsync()
    // {
    //     await context.SaveChangesAsync();
    // }
}