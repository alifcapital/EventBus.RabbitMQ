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
    public async Task PublishAsync(IOutboxEvent @event, string eventPath)
    {
        if(eventPublisher == null)
            throw new EventBusException("There is an outbox event ready to be published through the message broker, but RabbitMQ is not enabled.");
        
        eventPublisher.Publish((IPublishEvent)@event);
        await Task.CompletedTask;
    }
}