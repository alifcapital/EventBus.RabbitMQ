using EventBus.RabbitMQ.Publishers.Managers;
using EventBus.RabbitMQ.Subscribers.Managers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EventBus.RabbitMQ.BackgroundServices;

/// <summary>
/// The background service to start preparing publisher exchanges and subscriber queues.
/// And also print loaded publisher and subscriber information to the logger.
/// </summary>
internal class StartEventBusServices(
    IEventSubscriberCollector subscriberCollector,
    IEventPublisherCollector publisherCollector,
    ILogger<StartEventBusServices> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await publisherCollector.CreateExchangeForPublishersAsync(stoppingToken);
            await subscriberCollector.CreateConsumerForEachQueueAndStartReceivingEventsAsync(stoppingToken);
            
            publisherCollector.PrintLoadedPublishersInformation();
            subscriberCollector.PrintLoadedSubscribersInformation();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error while configuring publisher and subscriber of the RabbitMQ.");
        }
    }

    /// <summary>
    /// Stops receiving events before the application disposes its services, so the events being received while
    /// stopping are not handled with the disposed services. Unacknowledged events are redelivered by RabbitMQ.
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await subscriberCollector.StopReceivingEventsAsync(cancellationToken);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Error while stopping the RabbitMQ consumers.");
        }

        await base.StopAsync(cancellationToken);
    }
}