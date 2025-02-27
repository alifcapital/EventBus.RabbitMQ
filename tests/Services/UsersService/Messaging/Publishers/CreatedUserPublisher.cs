using EventBus.RabbitMQ.Publishers.Managers;
using EventStorage.Outbox.Providers.EventProviders;
using UsersService.Messaging.Events.Publishers;

namespace UsersService.Messaging.Publishers;

public class CreatedUserPublisher(IEventPublisherManager eventPublisher) : IMessageBrokerEventPublisher<UserCreated>
{
    public async Task PublishAsync(UserCreated @event, string eventPath)
    {
        eventPublisher.Publish(@event);
        //Add you logic
        await Task.CompletedTask;
    }
}