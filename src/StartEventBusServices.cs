using EventBus.RabbitMQ.Publishers.Managers;
using EventBus.RabbitMQ.Subscribers.Managers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EventBus.RabbitMQ;

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
            await publisherCollector.CreateExchangeForPublishersAsync();
            await subscriberCollector.CreateConsumerForEachQueueAndStartReceivingEventsAsync();
            
            publisherCollector.PrintLoadedPublishersInformation();
            subscriberCollector.PrintLoadedSubscribersInformation();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error while configuring publisher and subscriber of the RabbitMQ.");
        }
    }
}