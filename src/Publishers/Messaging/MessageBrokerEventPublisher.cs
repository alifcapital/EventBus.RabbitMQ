using EventBus.RabbitMQ.Exceptions;
using EventBus.RabbitMQ.Publishers.Managers;
using EventBus.RabbitMQ.Publishers.Models;
using EventStorage.Outbox.Models;
using EventStorage.Outbox.Providers;

namespace EventBus.RabbitMQ.Publishers.Messaging;

/// <summary>
/// The implementation of the message broker event publisher to publish an event to the RabbitMQ message broker when the application is configured to use the outbox pattern.
/// </summary>
/// <param name="eventPublisher">The event publisher manager to publish the event to the RabbitMQ message broker.</param>
internal class MessageBrokerEventPublisher(IEventPublisherManager eventPublisher = null) : IMessageBrokerEventPublisher
{
    public async Task PublishAsync(IOutboxEvent outboxEvent)
    {
        if (eventPublisher == null)
            throw new EventBusException(
                "There is an outbox event ready to be published through the message broker, but RabbitMQ is not enabled.");

        if (outboxEvent is not IPublishEvent publishEvent)
            return; //TODO: https://linear.app/alif-techtj/issue/ACM1-574 The global event publisher should not be executed if the event is not a publish event.

        eventPublisher.Publish(publishEvent);
        await Task.CompletedTask;
    }
}