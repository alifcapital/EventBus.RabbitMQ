using EventBus.RabbitMQ.Publishers.Managers;
using EventBus.RabbitMQ.Publishers.Models;
using EventStorage.Outbox.Models;
using EventStorage.Outbox.Providers;
using Microsoft.Extensions.Logging;

namespace EventBus.RabbitMQ.Publishers.Messaging;

/// <summary>
/// The implementation of the message broker event publisher to publish an event to the RabbitMQ message broker when the application is configured to use the outbox pattern.
/// </summary>
/// <param name="eventPublisher">The event publisher manager to publish the event to the RabbitMQ message broker.</param>
public class MessageBrokerEventPublisher(IEventPublisherManager eventPublisher, ILogger<MessageBrokerEventPublisher> logger) : IMessageBrokerEventPublisher
{
    public async Task PublishAsync(IOutboxEvent outboxEvent)
    {
        if (outboxEvent is not IPublishEvent publishEvent)
        {
            logger.LogWarning("The publishing outbox event ({OutboxEventType}) with ID {OutboxEventId} is not a RabbitMQ event type. Skipping publishing to message broker.", outboxEvent.GetType().Name, outboxEvent.EventId);
            return;
        }
        
        await eventPublisher.PublishAsync(publishEvent, CancellationToken.None);
    }
}