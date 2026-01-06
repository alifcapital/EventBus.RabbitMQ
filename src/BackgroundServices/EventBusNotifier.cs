using EventBus.RabbitMQ.Configurations;
using EventStorage.Configurations;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EventBus.RabbitMQ.BackgroundServices;

/// <summary>
/// The background service for notifying about EventBus.RabbitMq's configuration status.
/// </summary>
internal class EventBusNotifier(
    RabbitMqOptions rabbitMqOptions,
    InboxAndOutboxSettings eventStorageOptions,
    ILogger<EventBusNotifier> logger)
    : BackgroundService
{
    #region ExecuteAsync

    /// <summary>
    /// Shows informational logs if the RabbitMQ functionality is disabled or if the Inbox functionality is disabled even using an Inbox is enabled in RabbitMQ settings.
    /// </summary>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!rabbitMqOptions.IsEnabled)
            logger.LogInformation("Since the RabbitMQ functionality is disabled, publishing and subscribing events will be skipped.");

        if(rabbitMqOptions.IsEnabled && rabbitMqOptions.UseInbox && !eventStorageOptions.Inbox.IsEnabled)
            logger.LogWarning("Since the Inbox functionality is disabled, even using an Inbox is enabled in RabbitMQ settings, receiving events from the RabbitMQ message broker will be handled without an Inbox.");

        return Task.CompletedTask;
    }

    #endregion
}