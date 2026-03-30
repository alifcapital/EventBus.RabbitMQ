using EventBus.RabbitMQ.Configurations;
using EventBus.RabbitMQ.Exceptions;
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
    /// Shows informational logs if the RabbitMQ functionality is disabled, or throws if Inbox/Outbox is enabled in RabbitMQ settings but the corresponding EventStorage feature is disabled.
    /// </summary>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!rabbitMqOptions.IsEnabled)
        {
            logger.LogInformation(
                "Since the RabbitMQ functionality is disabled, publishing and subscribing events will be skipped.");
        }
        else
        {
            if (rabbitMqOptions.UseInbox && !eventStorageOptions.Inbox.IsEnabled)
                throw new EventBusException(
                    "Using an Inbox functionality is enabled in RabbitMQ settings, but the Inbox functionality itself is disabled. To use an Inbox for storing all received events before handling, please enable the Inbox functionality in Event Storage settings.");

            if (rabbitMqOptions.UseOutbox && !eventStorageOptions.Outbox.IsEnabled)
                throw new EventBusException(
                    "Using an Outbox functionality is enabled in RabbitMQ settings, but the Outbox functionality itself is disabled. To use an Outbox for storing all publishing events before send, please enable the Outbox functionality in Event Storage settings.");
        }

        return Task.CompletedTask;
    }

    #endregion
}