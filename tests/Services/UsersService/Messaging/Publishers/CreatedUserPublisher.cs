using EventBus.RabbitMQ.Publishers.Managers;
using EventStorage.Outbox.Providers.EventProviders;
using UsersService.Messaging.Events.Publishers;

namespace UsersService.Messaging.Publishers;

public class CreatedUserPublisher : IMessageBrokerEventPublisher<UserCreated>
{
    private readonly IEventPublisherManager _eventPublisher;
    
    public CreatedUserPublisher(IEventPublisherManager eventPublisher)
    {
        _eventPublisher = eventPublisher;
    }

    public async Task PublishAsync(UserCreated @event, string eventPath)
    {
        _eventPublisher.Publish(@event);
        //Add you logic
        await Task.CompletedTask;
    }
}