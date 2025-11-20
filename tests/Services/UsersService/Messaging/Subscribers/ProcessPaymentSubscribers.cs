using EventBus.RabbitMQ.Subscribers.Models;
using EventStorage.Outbox.Managers;
using UsersService.Infrastructure;
using UsersService.Messaging.Events.Publishers;
using UsersService.Messaging.Events.Subscribers;

namespace UsersService.Messaging.Subscribers;

public class ProcessPaymentSubscribers(
    UserContext context,
    IOutboxEventManager outboxEventManager) :
    IEventSubscriber<ProcessPayment>,
    IEventSubscriber<RefundPayment>
{
    public async Task HandleAsync(ProcessPayment @event)
    {
        var user = context.Users.FirstOrDefault(u => u.Email == @event.CustomerEmail);
        if (user is null)
        {
            outboxEventManager.Collect(new OrderFailed
            {
                OrderId = @event.OrderId,
                Reason = "Customer not found"
            });
        }
        else if (user.Balance < @event.Amount)
        {
            outboxEventManager.Collect(new OrderFailed
            {
                OrderId = @event.OrderId,
                Reason = "Customer does not have enough balance"
            });
        }
        else
        {
            user.Balance -= @event.Amount;
            context.Users.Update(user);

            outboxEventManager.Collect(new PaymentProcessed
            {
                OrderId = @event.OrderId
            });
            await context.SaveChangesAsync();
        }
    }

    public async Task HandleAsync(RefundPayment @event)
    {
        var user = context.Users.Single(u => u.Email == @event.CustomerEmail);
        user.Balance += @event.Amount;
        context.Users.Update(user);
        await context.SaveChangesAsync();
    }
}