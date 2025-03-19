using EventBus.RabbitMQ.Publishers.Managers;
using EventStorage.Outbox.Providers.EventProviders;
using UsersService.Messaging.Events.Publishers;

namespace UsersService.Messaging.Publishers;

public class CreatedUserPublisher(IEventPublisherManager eventPublisher) : ISmsEventPublisher<UserCreated>
{
    public async Task PublishAsync(UserCreated userCreated)
    {
        await eventPublisher.PublishAsync(userCreated);
    }
}