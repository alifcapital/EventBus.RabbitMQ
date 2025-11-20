using EventBus.RabbitMQ.Subscribers.Models;
using UsersService.Infrastructure;
using UsersService.Messaging.Events.Publishers;
using UsersService.Messaging.Events.Subscribers;

namespace UsersService.Messaging.Subscribers;

public class ProcessPaymentSubscriber(UserContext context) : IEventSubscriber<ProcessPayment>
{
    public async Task HandleAsync(ProcessPayment @event)
    {
        var user = context.Users.FirstOrDefault(u => u.Email == @event.CustomerEmail);
        if (user is null)
        {
            
        }
        else
        {
            
        }
        
        
        await Task.CompletedTask;
    }
}