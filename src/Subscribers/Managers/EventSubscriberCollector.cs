using System.Diagnostics;
using EventBus.RabbitMQ.Configurations;
using EventBus.RabbitMQ.Instrumentation.Trace;
using EventBus.RabbitMQ.Subscribers.Consumers;
using EventBus.RabbitMQ.Subscribers.Models;
using EventBus.RabbitMQ.Subscribers.Options;
using EventStorage.Inbox.EventArgs;
using EventStorage.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EventBus.RabbitMQ.Subscribers.Managers;

internal class EventSubscriberCollector(RabbitMqOptions defaultSettings, IServiceProvider serviceProvider)
    : IEventSubscriberCollector
{
    /// <summary>
    /// The event to be executed before executing the subscriber of the received event.
    /// </summary>
    public static event EventHandler<SubscribedMessageBrokerEventArgs> ExecutingSubscribedEvent;

    /// <summary>
    /// Dictionary collection to store all event and event handler information
    /// </summary>
    private static readonly Dictionary<string, SubscribersInformation>
        Subscribers = new();

    /// <summary>
    /// List of consumers for each unique a queue for different virtual host 
    /// </summary>
    private readonly Dictionary<string, IEventConsumerService> _eventConsumers = new();

    #region AddSubscriber
    
    public void AddSubscriber<TEvent, TEventHandler>(Action<EventSubscriberOptions> options = null)
        where TEvent : class, ISubscribeEvent
        where TEventHandler : class, IEventSubscriber<TEvent>
    {
        var eventType = typeof(TEvent);
        var handlerType = typeof(TEventHandler);
        if (!Subscribers.TryGetValue(eventType.Name, out var subscribersInformation))
        {
            subscribersInformation = new SubscribersInformation
            {
                EventTypeName = eventType.Name,
                Settings = new EventSubscriberOptions()
            };
            Subscribers.Add(eventType.Name, subscribersInformation);
        }
        
        options?.Invoke(subscribersInformation.Settings);
        subscribersInformation.AddSubscriberIfNotExists(eventType, handlerType);
    }

    /// <summary>
    /// Registers a subscriber 
    /// </summary>
    /// <param name="typeOfSubscriber">Event type which we want to subscribe</param>
    /// <param name="typeOfHandler">Handler type of the event which we want to receive event</param>
    /// <param name="subscriberSettings">Settings of subscriber</param>
    public void AddSubscriber(Type typeOfSubscriber, Type typeOfHandler, EventSubscriberOptions subscriberSettings)
    {
        if (Subscribers.TryGetValue(typeOfSubscriber.Name, out var subscribersInformation))
        {
            subscribersInformation.Settings = subscriberSettings;
        }
        else
        {
            subscribersInformation = new SubscribersInformation
            {
                EventTypeName = typeOfSubscriber.Name,
                Settings = subscriberSettings
            };
            Subscribers.Add(typeOfSubscriber.Name, subscribersInformation);
        }
        
        subscribersInformation.AddSubscriberIfNotExists(typeOfSubscriber, typeOfHandler);
    }
    
    #endregion
    
    #region SetVirtualHostAndOwnSettingsOfSubscribers

    /// <summary>
    /// Setting the virtual host and other unassigned settings of subscribers
    /// </summary>
    public void SetVirtualHostAndOwnSettingsOfSubscribers(Dictionary<string, RabbitMqHostSettings> virtualHostsSettings)
    {
        foreach (var (eventTypeName, subscribersInformation) in Subscribers)
        {
            var eventSettings = subscribersInformation.Settings;
            var virtualHostSettings = string.IsNullOrEmpty(eventSettings.VirtualHostKey)
                ? defaultSettings
                : virtualHostsSettings.GetValueOrDefault(eventSettings.VirtualHostKey, defaultSettings);
            eventSettings.SetVirtualHostAndUnassignedSettings(virtualHostSettings, eventTypeName);
        }
    }
    
    #endregion
    
    #region CreateConsumerForEachQueueAndStartReceivingEvents

    public void CreateConsumerForEachQueueAndStartReceivingEvents()
    {
        var eventConsumerCreator = serviceProvider.GetRequiredService<IEventConsumerServiceCreator>();
        foreach (var (_, eventInfo) in Subscribers)
        {
            var consumerId =
                $"{eventInfo.Settings.VirtualHostSettings.VirtualHost}-{eventInfo.Settings.QueueName}";
            if (!_eventConsumers.TryGetValue(consumerId, value: out var eventConsumer))
            {
                eventConsumer =
                    eventConsumerCreator.Create(eventInfo.Settings, serviceProvider, defaultSettings.UseInbox);
                _eventConsumers.Add(consumerId, eventConsumer);
            }

            eventConsumer.AddSubscriber(eventInfo);
        }

        foreach (var consumer in _eventConsumers)
        {
            try
            {
                consumer.Value.CreateChannelAndSubscribeReceiver();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
    
    #endregion

    #region On exceuting subscribed event

    /// <summary>
    /// Invokes the ExecutingReceivedEvent event to be able to execute the event before the subscriber.
    /// </summary>
    /// <param name="event">Executing an event</param>
    /// <param name="virtualHostName">The name of virtual host to being able to get a system name that the event published by it.</param>
    /// <param name="serviceProvider">The IServiceProvider used to resolve dependencies from the scope.</param>
    public static void OnExecutingSubscribedEvent(ISubscribeEvent @event, string virtualHostName,
        IServiceProvider serviceProvider)
    {
        if (ExecutingSubscribedEvent is null)
            return;

        var systemName = virtualHostName.TrimStart('/');
        var eventArgs = new SubscribedMessageBrokerEventArgs(@event, systemName, serviceProvider);
        ExecutingSubscribedEvent.Invoke(null, eventArgs);
    }

    /// <summary>
    /// For handling the ExecutingInboxEvent event and execute the ExecutingSubscribedEvent event if the  
    /// </summary>
    public static void HandleExecutingInboxEvent(object sender, InboxEventArgs e)
    {
        if (e.ProviderType == EventProviderType.MessageBroker)
        {
            if (e.Event is not ISubscribeEvent @event)
                return;

            var eventTypeName = @event.GetType().Name;
            var virtualHostName = Subscribers.TryGetValue(eventTypeName, out var info)
                ? info.Settings.VirtualHostSettings.VirtualHost
                : string.Empty;

            OnExecutingSubscribedEvent(@event, virtualHostName, e.ServiceProvider);
        }
    }

    #endregion
    
    #region PrintLoadedSubscribersInformation
    
    public void PrintLoadedSubscribersInformation()
    {
        var logger = serviceProvider.GetRequiredService<ILogger<IEventSubscriberCollector>>();
        var loadedSubscribersCount = Subscribers.Count;
        using var activity = EventBusTraceInstrumentation.StartActivity(
            $"MQ: Total {loadedSubscribersCount} subscribers are loaded.", ActivityKind.Server);
        logger.LogInformation("Total {loadedSubscribersCount} subscribers are loaded.", loadedSubscribersCount);
        
        foreach (var (eventName, subscribersInformation) in Subscribers)
        {
            var eventSettings = subscribersInformation.Settings;
            var subscriberHandlersCount = subscribersInformation.Subscribers.Count;
            logger.LogDebug(
                "Loaded subscriber: EventName='{EventName}', VirtualHost='{VirtualHost}', ExchangeName='{ExchangeName}', ExchangeType='{ExchangeType}', RoutingKey='{RoutingKey}', PropertyNamingPolicy='{PropertyNamingPolicy}', QueueName='{QueueName}', HandlersCount='{HandlersCount}'",
                eventName,
                eventSettings.VirtualHostSettings.VirtualHost,
                eventSettings.VirtualHostSettings.ExchangeName,
                eventSettings.VirtualHostSettings.ExchangeType,
                eventSettings.RoutingKey,
                eventSettings.PropertyNamingPolicy,
                eventSettings.QueueName,
                subscriberHandlersCount);

            if (subscriberHandlersCount == 0)
                logger.LogWarning("No subscriber handlers are loaded for the event '{EventName}'", eventName);
        }
        
    }
    
    #endregion
}