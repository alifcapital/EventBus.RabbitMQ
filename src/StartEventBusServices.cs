using EventBus.RabbitMQ.Publishers.Managers;
using EventBus.RabbitMQ.Subscribers.Managers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EventBus.RabbitMQ;

internal class StartEventBusServices(
    IEventSubscriberCollector subscriberCollector,
    IEventPublisherCollector publisherCollector,
    ILogger<StartEventBusServices> logger)
    : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            publisherCollector.CreateExchangeForPublishers();
            subscriberCollector.CreateConsumerForEachQueueAndStartReceivingEvents();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error while configuring publisher and subscriber of the RabbitMQ.");
        }
        
        return Task.CompletedTask;
    }
}