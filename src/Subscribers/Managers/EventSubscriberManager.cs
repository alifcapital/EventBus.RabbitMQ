using EventBus.RabbitMQ.Configurations;
using EventBus.RabbitMQ.Subscribers.Consumers;
using EventBus.RabbitMQ.Subscribers.Models;
using EventBus.RabbitMQ.Subscribers.Options;
using Microsoft.Extensions.DependencyInjection;

namespace EventBus.RabbitMQ.Subscribers.Managers;

internal class EventSubscriberManager(RabbitMqOptions defaultSettings, IServiceProvider serviceProvider)
    : IEventSubscriberManager
{
    /// <summary>
    /// Dictionary collection to store all event and event handler information
    /// </summary>
    private readonly Dictionary<string, (Type eventType, Type eventHandlerType, EventSubscriberOptions eventSettings)>
        _subscribers = new();

    /// <summary>
    /// List of consumers for each unique a queue for different virtual host 
    /// </summary>
    private readonly Dictionary<string, IEventConsumerService> _eventConsumers = new();

    public void AddSubscriber<TEvent, TEventHandler>(Action<EventSubscriberOptions> options = null)
        where TEvent : class, ISubscribeEvent
        where TEventHandler : class, IEventSubscriber<TEvent>
    {
        var eventType = typeof(TEvent);
        if (_subscribers.TryGetValue(eventType.Name, out var info))
        {
            options?.Invoke(info.eventSettings);
        }
        else
        {
            var settings = new EventSubscriberOptions();
            options?.Invoke(settings);

            var handlerType = typeof(TEventHandler);
            _subscribers.Add(eventType.Name, (eventType, handlerType, settings));
        }
    }

    /// <summary>
    /// Registers a subscriber 
    /// </summary>
    /// <param name="typeOfSubscriber">Event type which we want to subscribe</param>
    /// <param name="typeOfHandler">Handler type of the event which we want to receive event</param>
    /// <param name="subscriberSettings">Settings of subscriber</param>
    public void AddSubscriber(Type typeOfSubscriber, Type typeOfHandler, EventSubscriberOptions subscriberSettings)
    {
        if (_subscribers.TryGetValue(typeOfSubscriber.Name, out var info))
            _subscribers[typeOfSubscriber.Name] = (info.eventType, info.eventHandlerType, subscriberSettings);
        else
            _subscribers.Add(typeOfSubscriber.Name, (typeOfSubscriber, typeOfHandler, subscriberSettings));
    }

    /// <summary>
    /// Setting the virtual host and other unassigned settings of subscribers
    /// </summary>
    public void SetVirtualHostAndOwnSettingsOfSubscribers(Dictionary<string, RabbitMqHostSettings> virtualHostsSettings)
    {
        foreach (var (eventTypeName, (_, _, eventSettings)) in _subscribers)
        {
            var virtualHostSettings = string.IsNullOrEmpty(eventSettings.VirtualHostKey) ? defaultSettings : virtualHostsSettings.GetValueOrDefault(eventSettings.VirtualHostKey, defaultSettings);
            eventSettings.SetVirtualHostAndUnassignedSettings(virtualHostSettings, eventTypeName);
        }
    }

    public void CreateConsumerForEachQueueAndStartReceivingEvents()
    {
        var eventConsumerCreator = serviceProvider.GetRequiredService<IEventConsumerServiceCreator>();
        foreach (var (_, eventInfo) in _subscribers)
        {
            var consumerId = $"{eventInfo.eventSettings.VirtualHostSettings.VirtualHost}-{eventInfo.eventSettings.QueueName}";
            if (!_eventConsumers.TryGetValue(consumerId, value: out IEventConsumerService eventConsumer))
            {
                eventConsumer = eventConsumerCreator.Create(eventInfo.eventSettings, serviceProvider, defaultSettings.UseInbox);
                _eventConsumers.Add(consumerId, eventConsumer);
            }

            eventConsumer.AddSubscriber(eventInfo);
        }

        foreach (var consumer in _eventConsumers)
            consumer.Value.StartAndSubscribeReceiver();
    }
}