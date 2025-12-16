using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using EventBus.RabbitMQ.Instrumentation;
using EventBus.RabbitMQ.Instrumentation.Trace;
using EventBus.RabbitMQ.Publishers.Models;
using EventStorage.Instrumentation;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace EventBus.RabbitMQ.Publishers.Managers;

internal class EventPublisherManager(
    ILogger<EventPublisherManager> logger,
    IEventPublisherCollector eventPublisherCollector = null
) : IEventPublisherManager
{
    private readonly ConcurrentDictionary<Guid, IPublishEvent> _eventsToPublish = [];

    #region PublishAsync

    public async Task PublishAsync<TPublishEvent>(TPublishEvent publishEvent, CancellationToken cancellationToken)
        where TPublishEvent : class, IPublishEvent
    {
        await PublishEventToRabbitMqAsync(publishEvent, cancellationToken);
    }

    #endregion

    #region Collect

    public void Collect<TPublishEvent>(TPublishEvent publishEvent) where TPublishEvent : class, IPublishEvent
    {
        if (!_eventsToPublish.ContainsKey(publishEvent.EventId))
            _eventsToPublish.TryAdd(publishEvent.EventId, publishEvent);
    }

    #endregion

    #region CleanCollectedEvents

    public void CleanCollectedEvents()
    {
        _eventsToPublish.Clear();
    }

    #endregion

    #region PublishCollectedEvents

    /// <summary>
    /// Publish all collected events to the RabbitMQ.
    /// </summary>
    private void PublishCollectedEvents()
    {
        foreach (var eventToPublish in _eventsToPublish.Values)
            PublishEventToRabbitMq(eventToPublish);

        CleanCollectedEvents();
    }

    #endregion

    #region PublishEventToRabbitMq

    /// <summary>
    /// Publish an event to the RabbitMQ. If the RabbitMQ is not enabled, it will log a warning and return.
    /// </summary>
    private void PublishEventToRabbitMq<TPublishEvent>(TPublishEvent publishEvent)
        where TPublishEvent : class, IPublishEvent
    {
        PublishEventToRabbitMqAsync(publishEvent, CancellationToken.None).GetAwaiter().GetResult();
    }

    private async Task PublishEventToRabbitMqAsync<TPublishEvent>(TPublishEvent publishEvent,
        CancellationToken cancellationToken)
        where TPublishEvent : class, IPublishEvent
    {
        if (eventPublisherCollector == null)
        {
            logger.LogWarning(
                "There is an event ready to be published through the message broker, but RabbitMQ is not enabled.");
            return;
        }

        var eventTypeName = publishEvent.GetType().Name;
        try
        {
            var publisherType = publishEvent.GetType();
            var eventSettings = eventPublisherCollector.GetPublisherSettings(publishEvent);
            eventTypeName = eventSettings.EventTypeName;

            logger.LogDebug("MQ: Publishing event '{EventName}' (ID: {EventId})", eventTypeName, publishEvent.EventId);

            var traceParentId = Activity.Current?.Id;
            using var activity = EventBusTraceInstrumentation.StartActivity(
                $"MQ: Publishing event '{eventTypeName}' (ID: {publishEvent.EventId})", ActivityKind.Producer,
                traceParentId);

            await using var channel =
                await eventPublisherCollector.CreateRabbitMqChannel(eventSettings, cancellationToken);

            var properties = new BasicProperties
            {
                MessageId = publishEvent.EventId.ToString(),
                Type = eventTypeName
            };

            var headers = new Dictionary<string, object>
            {
                {
                    EventStorageInvestigationTagNames.EventNamingPolicyTypeTag,
                    eventSettings.PropertyNamingPolicy?.ToString()
                }
            };
            if (activity is not null)
                headers.Add(EventBusTraceInstrumentation.TraceParentIdKey, activity.Id);

            if (publishEvent.Headers?.Count > 0)
            {
                foreach (var item in publishEvent.Headers)
                    headers.Add(item.Key, item.Value);
            }

            var jsonSerializerSetting = eventSettings.GetJsonSerializer();
            var payload = JsonSerializer.Serialize(publishEvent, publisherType, jsonSerializerSetting);

            if (activity is not null)
            {
                if (EventBusTraceInstrumentation.ShouldAttachEventPayload)
                    activity.AddEvent(new ActivityEvent($"{EventBusInvestigationTagNames.EventPayloadTag}: {payload}"));

                if (EventBusTraceInstrumentation.ShouldAttachEventHeaders)
                {
                    var headersAsJson = JsonSerializer.Serialize(headers);
                    activity.AddEvent(
                        new ActivityEvent($"{EventBusInvestigationTagNames.EventHeadersTag}: {headersAsJson}"));
                }
            }

            var messageBody = Encoding.UTF8.GetBytes(payload);
            properties.Headers = headers;
            await channel.BasicPublishAsync(
                exchange: eventSettings.VirtualHostSettings.ExchangeName,
                routingKey: eventSettings.RoutingKey,
                mandatory: false,
                basicProperties: properties,
                body: messageBody,
                cancellationToken: cancellationToken
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while publishing RabbitMQ event '{EventName}'.", eventTypeName);
            throw;
        }
    }

    #endregion

    #region Dispose

    private bool _disposed;

    public void Dispose()
    {
        Disposing();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Publish all collected events to the RabbitMQ before disposing the object.
    /// </summary>
    private void Disposing()
    {
        if (_disposed) return;

        PublishCollectedEvents();

        _disposed = true;
    }

    ~EventPublisherManager()
    {
        Disposing();
    }

    #endregion
}