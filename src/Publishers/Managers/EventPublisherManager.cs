using System.Diagnostics;
using System.Text;
using System.Text.Json;
using EventBus.RabbitMQ.Configurations;
using EventBus.RabbitMQ.Connections;
using EventBus.RabbitMQ.Instrumentation.Trace;
using EventBus.RabbitMQ.Publishers.Models;
using EventBus.RabbitMQ.Publishers.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace EventBus.RabbitMQ.Publishers.Managers;

internal class EventPublisherManager(IServiceProvider serviceProvider) : IEventPublisherManager
{
    private readonly RabbitMqOptions _defaultSettings = serviceProvider.GetRequiredService<RabbitMqOptions>();

    private readonly ILogger<EventPublisherManager> _logger =
        serviceProvider.GetRequiredService<ILogger<EventPublisherManager>>();

    private readonly Dictionary<string, EventPublisherOptions> _publishersConnectionInfo = new();
    private readonly Dictionary<string, IRabbitMqConnection> _openedRabbitMqConnections = new();

    private readonly IRabbitMqConnectionCreator _rabbitMqConnectionCreator =
        serviceProvider.GetRequiredService<IRabbitMqConnectionCreator>();

    #region AddPublisher

    /// <summary>
    /// Registers a publisher.
    /// </summary>
    /// <param name="options">The options specific to the publisher, if any.</param>
    public void AddPublisher<TPublisher>(Action<EventPublisherOptions> options = null)
        where TPublisher : class, IPublishEvent
    {
        var publisherName = typeof(TPublisher).Name;
        if (_publishersConnectionInfo.TryGetValue(publisherName, out var settings))
        {
            options?.Invoke(settings);
        }
        else
        {
            settings = new EventPublisherOptions();
            options?.Invoke(settings);

            _publishersConnectionInfo.Add(publisherName, settings);
        }
    }

    /// <summary>
    /// Registers a publisher.
    /// </summary>
    /// <param name="typeOfPublisher">The type of the publisher.</param>
    /// <param name="publisherSettings">The options specific to the publisher, if any.</param>
    public void AddPublisher(Type typeOfPublisher, EventPublisherOptions publisherSettings)
    {
        _publishersConnectionInfo[typeOfPublisher.Name] = publisherSettings;
    }

    #endregion

    #region SetVirtualHostAndOwnSettingsOfPublishers

    /// <summary>
    /// Setting the virtual host and other unassigned settings of publishers
    /// </summary>
    public void SetVirtualHostAndOwnSettingsOfPublishers(Dictionary<string, RabbitMqHostSettings> virtualHostsSettings)
    {
        foreach (var (eventTypeName, eventSettings) in _publishersConnectionInfo)
        {
            var virtualHostSettings = string.IsNullOrEmpty(eventSettings.VirtualHostKey)
                ? _defaultSettings
                : virtualHostsSettings.GetValueOrDefault(eventSettings.VirtualHostKey, _defaultSettings);
            eventSettings.SetVirtualHostAndUnassignedSettings(virtualHostSettings, eventTypeName);
        }
    }

    #endregion

    #region CreateExchangeForPublishers

    /// <summary>
    /// Creating an exchange for each registered publisher and 
    /// </summary>
    public void CreateExchangeForPublishers()
    {
        var createdExchangeNames = new HashSet<string>();
        foreach (var (eventName, eventSettings) in _publishersConnectionInfo)
        {
            try
            {
                var exchangeId =
                    $"{eventSettings.VirtualHostSettings.VirtualHost}-{eventSettings.VirtualHostSettings.ExchangeName}";
                if (createdExchangeNames.Contains(exchangeId)) continue;

                using var channel = CreateRabbitMqChannel(eventSettings);
                channel.ExchangeDeclare(eventSettings.VirtualHostSettings.ExchangeName,
                    eventSettings.VirtualHostSettings.ExchangeType, durable: true,
                    autoDelete: false);

                createdExchangeNames.Add(exchangeId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while creating an exchange for {publisherName} publisher.", eventName);
            }
        }

        createdExchangeNames.Clear();
    }

    #endregion

    #region PublishAsync

    public async Task PublishAsync<TEvent>(TEvent @event) where TEvent : IPublishEvent
    {
        try
        {
            var traceParentId = Activity.Current?.Id;
            var publisherType = @event.GetType();
            var eventSettings = GetPublisherSettings(publisherType.Name);
            using var activity = EventBusTraceInstrumentation.StartActivity(
                $"Publishing '{eventSettings.EventTypeName}' event", ActivityKind.Producer, traceParentId);

            using var channel = CreateRabbitMqChannel(eventSettings);

            var properties = channel.CreateBasicProperties();
            properties.MessageId = @event.EventId.ToString();
            properties.Type = eventSettings.EventTypeName;

            var headers = new Dictionary<string, object>();
            properties.Headers = headers;
            if (activity is not null)
                headers.Add(EventBusTraceInstrumentation.TraceParentIdKey, activity.Id);

            if (@event.Headers?.Count > 0)
            {
                foreach (var item in @event.Headers)
                    headers.Add(item.Key, item.Value);
            }

            var jsonSerializerSetting = eventSettings.GetJsonSerializer();
            var payload = JsonSerializer.Serialize(@event, publisherType, jsonSerializerSetting);

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
                channel.BasicPublish(eventSettings.VirtualHostSettings.ExchangeName, eventSettings.RoutingKey, properties, messageBody);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while opening the RabbitMQ connection");
        }
    }

    #endregion

    #region Helper methods

    /// <summary>
    /// Creates RabbitMQ connection for the unique connection ID (VirtualHost+ExchangeName) and cache that.
    /// </summary>
    /// <param name="settings">Publisher setting to open connection</param>
    /// <returns>Returns create RabbitMQ connection</returns>
    private IRabbitMqConnection CreateRabbitMqConnection(EventPublisherOptions settings)
    {
        var connectionId = $"{settings.VirtualHostSettings.VirtualHost}-{settings.VirtualHostSettings.ExchangeName}";
        if (!_openedRabbitMqConnections.TryGetValue(connectionId, out var connection))
        {
            connection = _rabbitMqConnectionCreator.CreateConnection(settings, serviceProvider);
            _openedRabbitMqConnections.Add(connectionId, connection);
        }

        return connection;
    }

    /// <summary>
    /// Creates RabbitMQ connection for the unique connection ID (VirtualHost+ExchangeName) and cache that.
    /// </summary>
    /// <param name="settings">Publisher setting to open connection</param>
    /// <returns>Create and return chanel after creating and opening RabbitMQ connection</returns>
    private IModel CreateRabbitMqChannel(EventPublisherOptions settings)
    {
        var connection = CreateRabbitMqConnection(settings);
        return connection.CreateChannel();
    }

    private EventPublisherOptions GetPublisherSettings(string publisherName)
    {
        if (_publishersConnectionInfo.TryGetValue(publisherName, out var settings))
            return settings;

        throw new KeyNotFoundException(
            $"The reading {publisherName} publisher does not exist in the registered publishers list.");
    }

    #endregion
}