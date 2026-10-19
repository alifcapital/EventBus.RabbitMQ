using EventStorage.Outbox.Providers.EventProviders;

namespace EventBus.RabbitMQ.Tests.Domain;

/// <summary>
/// The specific outbox publisher of the <see cref="SimplePublishEvent"/>.
/// </summary>
public class SimplePublishEventPublisher : IMessageBrokerEventPublisher<SimplePublishEvent>
{
    public Task PublishAsync(SimplePublishEvent simplePublishEvent)
    {
        return Task.CompletedTask;
    }
}
