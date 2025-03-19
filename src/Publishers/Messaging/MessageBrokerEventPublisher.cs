using EventBus.RabbitMQ.Publishers.Managers;
using EventBus.RabbitMQ.Publishers.Models;
using EventStorage.Outbox.Models;
using EventStorage.Outbox.Providers;

namespace EventBus.RabbitMQ.Publishers.Messaging;

/// <summary>
/// The implementation of the message broker event publisher to publish an event to the RabbitMQ message broker when the application is configured to use the outbox pattern.
/// </summary>
/// <param name="eventPublisher">The event publisher manager to publish the event to the RabbitMQ message broker.</param>
internal class MessageBrokerEventPublisher(IEventPublisherManager eventPublisher) : IMessageBrokerEventPublisher
{
    public async Task PublishAsync(IOutboxEvent outboxEvent)
    {
        if (outboxEvent is not IPublishEvent publishEvent)
            return; //TODO: https://linear.app/alif-techtj/issue/ACM1-574 The global event publisher should not be executed if the event is not a publish event.

        await eventPublisher.PublishAsync(publishEvent);
    }
}