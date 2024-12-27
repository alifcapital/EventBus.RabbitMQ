using EventBus.RabbitMQ.Configurations;
using EventBus.RabbitMQ.Subscribers.Consumers;
using EventBus.RabbitMQ.Subscribers.Models;
using EventBus.RabbitMQ.Subscribers.Options;
using EventStorage.Inbox.EventArgs;
using EventStorage.Models;
using Microsoft.Extensions.DependencyInjection;

namespace EventBus.RabbitMQ.Subscribers.Managers;

internal class EventSubscriberManager(RabbitMqOptions defaultSettings, IServiceProvider serviceProvider)
    : IEventSubscriberManager
{
    /// <summary>
    /// The event to be executed before executing the subscriber of the received event.
    /// </summary>
    public static event EventHandler<SubscribedMessageBrokerEventArgs> ExecutingSubscribedEvent;

    /// <summary>
    /// Dictionary collection to store all event and event handler information
    /// </summary>
    private static readonly Dictionary<string, (Type eventType, Type eventHandlerType, EventSubscriberOptions eventSettings)>
        Subscribers = new();

    /// <summary>
    /// List of consumers for each unique a queue for different virtual host 
    /// </summary>
    private readonly Dictionary<string, IEventConsumerService> _eventConsumers = new();

    public void AddSubscriber<TEvent, TEventHandler>(Action<EventSubscriberOptions> options = null)
        where TEvent : class, ISubscribeEvent
        where TEventHandler : class, IEventSubscriber<TEvent>
    {
        var eventType = typeof(TEvent);
        if (Subscribers.TryGetValue(eventType.Name, out var info))
        {
            options?.Invoke(info.eventSettings);
        }
        else
        {
            var settings = new EventSubscriberOptions();
            options?.Invoke(settings);

            var handlerType = typeof(TEventHandler);
            Subscribers.Add(eventType.Name, (eventType, handlerType, settings));
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
        if (Subscribers.TryGetValue(typeOfSubscriber.Name, out var info))
            Subscribers[typeOfSubscriber.Name] = (info.eventType, info.eventHandlerType, subscriberSettings);
        else
            Subscribers.Add(typeOfSubscriber.Name, (typeOfSubscriber, typeOfHandler, subscriberSettings));
    }

    /// <summary>
    /// Setting the virtual host and other unassigned settings of subscribers
    /// </summary>
    public void SetVirtualHostAndOwnSettingsOfSubscribers(Dictionary<string, RabbitMqHostSettings> virtualHostsSettings)
    {
        foreach (var (eventTypeName, (_, _, eventSettings)) in Subscribers)
        {
            var virtualHostSettings = string.IsNullOrEmpty(eventSettings.VirtualHostKey)
                ? defaultSettings
                : virtualHostsSettings.GetValueOrDefault(eventSettings.VirtualHostKey, defaultSettings);
            eventSettings.SetVirtualHostAndUnassignedSettings(virtualHostSettings, eventTypeName);
        }
    }

    public void CreateConsumerForEachQueueAndStartReceivingEvents()
    {
        var eventConsumerCreator = serviceProvider.GetRequiredService<IEventConsumerServiceCreator>();
        foreach (var (_, eventInfo) in Subscribers)
        {
            var consumerId =
                $"{eventInfo.eventSettings.VirtualHostSettings.VirtualHost}-{eventInfo.eventSettings.QueueName}";
            if (!_eventConsumers.TryGetValue(consumerId, value: out var eventConsumer))
            {
                eventConsumer =
                    eventConsumerCreator.Create(eventInfo.eventSettings, serviceProvider, defaultSettings.UseInbox);
                _eventConsumers.Add(consumerId, eventConsumer);
            }

            eventConsumer.AddSubscriber(eventInfo);
        }

        foreach (var consumer in _eventConsumers)
            consumer.Value.StartAndSubscribeReceiver();
    }
    
    /// <summary>
    /// Invokes the ExecutingReceivedEvent event to be able to execute the event before the subscriber.
    /// </summary>
    /// <param name="event">Executing an event</param>
    /// <param name="virtualHostName">The name of virtual host to being able to get a system name that the event published by it.</param>
    public static void OnExecutingSubscribedEvent(ISubscribeEvent @event, string virtualHostName)
    {
        if (ExecutingSubscribedEvent is null)
            return;

        var systemName = virtualHostName.TrimStart('/');
        var eventArgs = new SubscribedMessageBrokerEventArgs
        {
            Event = @event,
            SystemName = systemName
        };

        ExecutingSubscribedEvent.Invoke(null, eventArgs);
    }

    /// <summary>
    /// For handling the ExecutingReceivedEvent event and execute the ExecutingSubscribedEvent event if the  
    /// </summary>
    public static void HandleExecutingReceivedEvent(object sender, ReceivedEventArgs e)
    {
        if (e.ProviderType == EventProviderType.MessageBroker)
        {
            if (e.Event is not ISubscribeEvent @event)
                return;

            var eventTypeName = @event.GetType().Name;
            var virtualHostName = Subscribers.TryGetValue(eventTypeName, out var info)
                ? info.eventSettings.VirtualHostSettings.VirtualHost
                : string.Empty;

            OnExecutingSubscribedEvent(@event, virtualHostName);
        }
    }
}