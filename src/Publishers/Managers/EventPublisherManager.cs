using System.Diagnostics;
using System.Text;
using System.Text.Json;
using EventBus.RabbitMQ.Exceptions;
using EventBus.RabbitMQ.Instrumentation.Trace;
using EventBus.RabbitMQ.Publishers.Models;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace EventBus.RabbitMQ.Publishers.Managers;

internal class EventPublisherManager(
    ILogger<EventPublisherManager> logger,
    IEventPublisherCollector eventPublisherCollector = null) : IEventPublisherManager
{
    public async Task PublishAsync<TPublishEvent>(TPublishEvent publishEvent)
        where TPublishEvent : class, IPublishEvent
    {
        if (eventPublisherCollector == null)
            throw new EventBusException(
                "There is an event ready to be published through the message broker, but RabbitMQ is not enabled.");

        try
        {
            var traceParentId = Activity.Current?.Id;
            var publisherType = publishEvent.GetType();
            var eventSettings = eventPublisherCollector.GetPublisherSettings(publishEvent);
            using var activity = EventBusTraceInstrumentation.StartActivity(
                $"Publishing '{eventSettings.EventTypeName}' event", ActivityKind.Producer, traceParentId);

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
            await Task.Run(() =>
            {
                channel.BasicPublish(eventSettings.VirtualHostSettings.ExchangeName, eventSettings.RoutingKey,
                    properties, messageBody);
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while opening the RabbitMQ connection");
        }
    }
}