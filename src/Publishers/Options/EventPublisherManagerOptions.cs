using EventBus.RabbitMQ.Publishers.Managers;
using EventBus.RabbitMQ.Publishers.Models;

namespace EventBus.RabbitMQ.Publishers.Options;

public class EventPublisherManagerOptions
{
    private readonly EventPublisherCollector _publisherCollector;

    internal EventPublisherManagerOptions(EventPublisherCollector publisherCollector)
    {
        _publisherCollector = publisherCollector;
    }

    /// <summary>
    /// Registers a publisher.
    /// </summary>
    /// <param name="eventPublisherOptions">The eventPublisherOptions specific to the publisher, if any.</param>
    /// <param name="publisherType">The class of specific event publisher (<c>IMessageBrokerEventPublisher&lt;TEvent&gt;</c>).
    ///  If it is not passed, the global <c>MessageBrokerEventPublisher</c> will be used.</param>
    public void AddPublisher<TPublisher>(Action<EventPublisherOptions> eventPublisherOptions = null,
        Type publisherType = null)
        where TPublisher : class, IPublishEvent
    {
        _publisherCollector.AddPublisher<TPublisher>(eventPublisherOptions, publisherType);
    }
}