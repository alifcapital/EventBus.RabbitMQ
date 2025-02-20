using EventBus.RabbitMQ.Subscribers.Models;
using OrdersService.Messaging.Events;

namespace OrdersService.Messaging.Subscribers;

public class UserUpdatedSubscriber : IEventSubscriber<UserUpdated>
{
    private readonly ILogger<UserUpdatedSubscriber> _logger;

    public UserUpdatedSubscriber(ILogger<UserUpdatedSubscriber> logger)
    {
        _logger = logger;
    }

    public async Task HandleAsync(UserUpdated @event)
    {
        if (@event.Headers?.TryGetValue("TraceId", out string traceId) == true)
        {
        }
        
        await Task.CompletedTask;
    }
}