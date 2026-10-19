using EventBus.RabbitMQ.Information.Models;

namespace EventBus.RabbitMQ.Information;

/// <summary>
/// Provides the resolved settings of all loaded publishers and subscribers, for example, to show them from an admin UI.
/// The connection secrets are not included.
/// </summary>
public interface IEventBusConfigurationProvider
{
    /// <summary>
    /// Gets the resolved settings of all loaded publishers.
    /// </summary>
    /// <returns>The loaded publishers, or an empty collection if the RabbitMQ is disabled.</returns>
    IReadOnlyCollection<EventPublisherInformation> GetPublishers();

    /// <summary>
    /// Gets the resolved settings of all loaded subscribers with their handlers.
    /// </summary>
    /// <returns>The loaded subscribers, or an empty collection if the RabbitMQ is disabled.</returns>
    IReadOnlyCollection<EventSubscriberInformation> GetSubscribers();
}
