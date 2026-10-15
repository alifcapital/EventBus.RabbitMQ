using EventBus.RabbitMQ.Information.Models;
using EventBus.RabbitMQ.Publishers.Managers;
using EventBus.RabbitMQ.Subscribers.Managers;
using Microsoft.Extensions.DependencyInjection;

namespace EventBus.RabbitMQ.Information;

/// <summary>
/// Reads the resolved settings from the collectors of publishers and subscribers. The collectors are registered only
/// when the RabbitMQ is enabled, so empty collections are returned when it is disabled.
/// </summary>
internal class EventBusConfigurationProvider(IServiceProvider serviceProvider) : IEventBusConfigurationProvider
{
    public IReadOnlyCollection<LoadedPublisherInformation> GetPublishers()
    {
        var publisherCollector = serviceProvider.GetService<IEventPublisherCollector>();

        return publisherCollector?.GetLoadedPublishers() ?? [];
    }

    public IReadOnlyCollection<LoadedSubscriberInformation> GetSubscribers()
    {
        var subscriberCollector = serviceProvider.GetService<IEventSubscriberCollector>();

        return subscriberCollector?.GetLoadedSubscribers() ?? [];
    }
}
