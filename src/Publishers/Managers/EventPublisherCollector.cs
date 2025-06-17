using EventBus.RabbitMQ.Configurations;
using EventBus.RabbitMQ.Connections;
using EventBus.RabbitMQ.Exceptions;
using EventBus.RabbitMQ.Publishers.Models;
using EventBus.RabbitMQ.Publishers.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace EventBus.RabbitMQ.Publishers.Managers;

internal class EventPublisherCollector(IServiceProvider serviceProvider) : IEventPublisherCollector
{
    private readonly RabbitMqOptions _defaultSettings = serviceProvider.GetRequiredService<RabbitMqOptions>();

    private readonly ILogger<EventPublisherCollector> _logger =
        serviceProvider.GetRequiredService<ILogger<EventPublisherCollector>>();

    private readonly Dictionary<string, EventPublisherOptions> _publishersConnectionInfo = new();
    private readonly Dictionary<string, IRabbitMqConnection> _openedRabbitMqConnections = new();

    private readonly IRabbitMqConnectionCreator _rabbitMqConnectionCreator =
        serviceProvider.GetRequiredService<IRabbitMqConnectionCreator>();

    #region AddPublisher

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
                var virtualHostSettings = eventSettings.VirtualHostSettings;
                var exchangeId = $"{virtualHostSettings.VirtualHost}-{virtualHostSettings.ExchangeName}";
                if (createdExchangeNames.Contains(exchangeId)) continue;

                using var channel = CreateRabbitMqChannel(eventSettings);
                channel.ExchangeDeclare(
                    exchange: virtualHostSettings.ExchangeName,
                    type: virtualHostSettings.ExchangeType, 
                    durable: true,
                    autoDelete: false,
                    arguments: virtualHostSettings.ExchangeArguments);

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
    
    #region GetPublisherSettings

    public EventPublisherOptions GetPublisherSettings<TPublishEvent>(TPublishEvent publishEvent)
        where TPublishEvent : class, IPublishEvent
    {
        var eventType = publishEvent.GetType().Name;
        if (_publishersConnectionInfo.TryGetValue(eventType, out var settings))
            return settings;

        throw new EventBusException(
            $"The {eventType} publishing event does not exist in the registered publishers list.");
    }

    #endregion

    #region CreateRabbitMqChannel

    public IModel CreateRabbitMqChannel(EventPublisherOptions settings)
    {
        var connection = CreateRabbitMqConnection(settings);
        return connection.CreateChannel();
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

    #endregion
}