using EventBus.RabbitMQ.Publishers.Managers;
using EventBus.RabbitMQ.Subscribers.Managers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EventBus.RabbitMQ;

internal class StartEventBusServices : BackgroundService
{
    private readonly IEventSubscriberManager _subscriberManager;
    private readonly IEventPublisherManager _publisherManager;
    private readonly ILogger<StartEventBusServices> _logger;

    public StartEventBusServices(IEventSubscriberManager subscriberManager, IEventPublisherManager publisherManager, ILogger<StartEventBusServices> logger)
    {
        _publisherManager = publisherManager;
        _subscriberManager = subscriberManager;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _publisherManager.CreateExchangeForPublishers();
            _subscriberManager.CreateConsumerForEachQueueAndStartReceivingEvents();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error while configuring publisher and subscriber of the RabbitMQ.");
        }
        
        return Task.CompletedTask;
    }
}