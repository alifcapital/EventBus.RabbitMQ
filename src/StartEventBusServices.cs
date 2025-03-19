using EventBus.RabbitMQ.Publishers.Managers;
using EventBus.RabbitMQ.Subscribers.Managers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EventBus.RabbitMQ;

internal class StartEventBusServices(
    IEventSubscriberManager subscriberManager,
    IEventPublisherManager publisherManager,
    ILogger<StartEventBusServices> logger)
    : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            publisherManager.CreateExchangeForPublishers();
            subscriberManager.CreateConsumerForEachQueueAndStartReceivingEvents();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error while configuring publisher and subscriber of the RabbitMQ.");
        }
        
        return Task.CompletedTask;
    }
}