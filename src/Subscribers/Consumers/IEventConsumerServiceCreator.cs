using EventBus.RabbitMQ.Subscribers.Options;

namespace EventBus.RabbitMQ.Subscribers.Consumers;

/// <summary>
/// The interface for creating event consumer services.
/// </summary>
internal interface IEventConsumerServiceCreator
{
    /// <summary>
    /// Creates an instance of IEventConsumerService based on the provided connection options, service provider, and inbox usage flag.
    /// </summary>
    /// <param name="connectionOptions">The options for creating an event consumer service.</param>
    /// <param name="serviceProvider">The service provider for dependency injection.</param>
    /// <param name="shouldUseInbox">Flag indicating whether to use inbox functionality.</param>
    /// <returns>Newly created instance of IEventConsumerService.</returns>
    public IEventConsumerService Create(EventSubscriberOptions connectionOptions, IServiceProvider serviceProvider, bool shouldUseInbox);
}