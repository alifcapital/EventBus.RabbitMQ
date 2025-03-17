using EventBus.RabbitMQ.Publishers.Managers;
using EventStorage.Outbox.Providers.EventProviders;
using UsersService.Messaging.Events.Publishers;

namespace UsersService.Messaging.Publishers;

public class CreatedUserPublisher(IEventPublisherManager eventPublisher) : IMessageBrokerEventPublisher<UserCreated>
{
    public async Task PublishAsync(UserCreated userCreated)
    {
        eventPublisher.Publish(userCreated);
        //Add you logic
        await Task.CompletedTask;
    }
}