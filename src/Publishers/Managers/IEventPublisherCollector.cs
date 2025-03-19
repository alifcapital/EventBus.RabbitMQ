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
    /// Creates RabbitMQ connection for the unique connection ID (VirtualHost+ExchangeName) and cache that.
    /// </summary>
    /// <param name="settings">Publisher setting to open connection</param>
    /// <returns>Create and return chanel after creating and opening RabbitMQ connection</returns>
    public IModel CreateRabbitMqChannel(EventPublisherOptions settings);

    /// <summary>
    /// Creating an exchange for each registered publisher and 
    /// </summary>
    internal void CreateExchangeForPublishers();
}