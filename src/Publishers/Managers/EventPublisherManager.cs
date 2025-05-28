﻿using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using EventBus.RabbitMQ.Instrumentation.Trace;
using EventBus.RabbitMQ.Publishers.Models;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace EventBus.RabbitMQ.Publishers.Managers;

internal class EventPublisherManager(
    ILogger<EventPublisherManager> logger,
    IEventPublisherCollector eventPublisherCollector = null) : IEventPublisherManager
{
    private readonly ConcurrentDictionary<Guid, IPublishEvent> _eventsToPublish = [];

    #region PublishAsync

    public async Task PublishAsync<TPublishEvent>(TPublishEvent publishEvent)
        where TPublishEvent : class, IPublishEvent
    {
        await Task.Run(() => { PublishEventToRabbitMq(publishEvent); });
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
        if (eventPublisherCollector == null)
        {
            logger.LogWarning(
                "There is an event ready to be published through the message broker, but RabbitMQ is not enabled.");
            return;
        }

        try
        {
            var traceParentId = Activity.Current?.Id;
            var publisherType = publishEvent.GetType();
            var eventSettings = eventPublisherCollector.GetPublisherSettings(publishEvent);
            using var activity = EventBusTraceInstrumentation.StartActivity(
                $"MQ: Publishing '{eventSettings.EventTypeName}' event", ActivityKind.Producer, traceParentId);

            using var channel = eventPublisherCollector.CreateRabbitMqChannel(eventSettings);

            var properties = channel.CreateBasicProperties();
            properties.MessageId = publishEvent.EventId.ToString();
            properties.Type = eventSettings.EventTypeName;

            var headers = new Dictionary<string, object>();
            properties.Headers = headers;
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
                    activity.AddEvent(new ActivityEvent($"{EventBusTraceInstrumentation.EventPayloadTag}: {payload}"));

                if (EventBusTraceInstrumentation.ShouldAttachEventHeaders)
                {
                    var headersAsJson = JsonSerializer.Serialize(headers);
                    activity.AddEvent(
                        new ActivityEvent($"{EventBusTraceInstrumentation.EventHeadersTag}: {headersAsJson}"));
                }
            }

            var messageBody = Encoding.UTF8.GetBytes(payload);
            channel.BasicPublish(eventSettings.VirtualHostSettings.ExchangeName, eventSettings.RoutingKey,
                properties, messageBody);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while opening the RabbitMQ connection or publishing the event.");
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