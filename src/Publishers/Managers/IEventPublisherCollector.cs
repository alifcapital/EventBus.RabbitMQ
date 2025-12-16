using EventBus.RabbitMQ.Publishers.Models;
using EventBus.RabbitMQ.Publishers.Options;
using RabbitMQ.Client;

namespace EventBus.RabbitMQ.Publishers.Managers;

/// <summary>
/// This interface for registering  publishers and getting the settings of the publishing event.
/// </summary>
internal interface IEventPublisherCollector
{
    /// <summary>
    /// Registers a publisher.
    /// </summary>
    /// <param name="options">The options specific to the publisher, if any.</param>
    public void AddPublisher<TPublishEvent>(Action<EventPublisherOptions> options = null)
        where TPublishEvent : class, IPublishEvent;

    /// <summary>
    /// Registers a publisher.
    /// </summary>
    /// <param name="typeOfPublisher">The type of the publisher.</param>
    /// <param name="publisherSettings">The options specific to the publisher, if any.</param>
    public void AddPublisher(Type typeOfPublisher, EventPublisherOptions publisherSettings);

    /// <summary>
    /// For getting the settings of the publishing event.
    /// </summary>
    public EventPublisherOptions GetPublisherSettings<TPublishEvent>(TPublishEvent publishEvent)
        where TPublishEvent : class, IPublishEvent;

    /// <summary>
    /// Creates RabbitMQ channel after creating and opening RabbitMQ connection.
    /// </summary>
    /// <param name="settings">Publisher setting to open connection</param>
    /// <returns>Newly created RabbitMQ channel</returns>
    public Task<IChannel> CreateRabbitMqChannel(EventPublisherOptions settings, CancellationToken cancellationToken);

    /// <summary>
    /// Creating an exchange for each registered publisher and 
    /// </summary>
    internal Task CreateExchangeForPublishersAsync(CancellationToken cancellationToken);

    /// <summary>
    /// For printing all loaded publishers information to the logger for investigation purposes.
    /// </summary>
    internal void PrintLoadedPublishersInformation();
}
