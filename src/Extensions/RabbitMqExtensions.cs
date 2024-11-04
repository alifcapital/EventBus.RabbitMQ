using System.Reflection;
using EventBus.RabbitMQ.Configurations;
using EventBus.RabbitMQ.Connections;
using EventBus.RabbitMQ.Publishers.Managers;
using EventBus.RabbitMQ.Publishers.Models;
using EventBus.RabbitMQ.Publishers.Options;
using EventBus.RabbitMQ.Subscribers.Consumers;
using EventBus.RabbitMQ.Subscribers.Managers;
using EventBus.RabbitMQ.Subscribers.Models;
using EventBus.RabbitMQ.Subscribers.Options;
using EventStorage.Configurations;
using EventStorage.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EventBus.RabbitMQ.Extensions;

public static class RabbitMqExtensions
{
    /// <summary>
    /// Register the RabbitMQ settings as EventBus
    /// </summary>
    /// <param name="services">BackgroundServices of DI</param>
    /// <param name="configuration">Configuration to get config</param>
    /// <param name="defaultOptions">Default settings of RabbitMQ. It will overwrite all other default settings or settings those come from the configuration</param>
    /// <param name="virtualHostSettingsOptions">Dictionary virtual host settings to use them for publishing or subscribing events. It will overwrite all other settings those come from the configuration</param>
    /// <param name="eventPublisherManagerOptions">Options to register publisher with the settings. It will overwrite existing publisher setting if exists</param>
    /// <param name="eventSubscriberManagerOptions">Options to register subscriber with the settings. It will overwrite existing subscriber setting if exists</param>
    /// <param name="eventStoreOptions">Options to overwrite default settings of Inbox and Outbox.</param>
    /// <param name="assemblies">Assemblies to find and load publisher and subscribers</param>
    public static void AddRabbitMqEventBus(this IServiceCollection services, IConfiguration configuration,
        Assembly[] assemblies,
        Action<RabbitMqOptions> defaultOptions = null,
        Action<Dictionary<string, RabbitMqHostSettings>> virtualHostSettingsOptions = null,
        Action<EventPublisherManagerOptions> eventPublisherManagerOptions = null,
        Action<EventSubscriberManagerOptions> eventSubscriberManagerOptions = null,
        Action<InboxAndOutboxOptions> eventStoreOptions = null)
    {
        services.AddEventStore(configuration, assemblies: assemblies, options: eventStoreOptions);

        var settings = configuration.GetSection(nameof(RabbitMqSettings)).Get<RabbitMqSettings>() ??
                       new RabbitMqSettings();
        LoadDefaultRabbitMqOptions(settings, defaultOptions);
        if (!settings.DefaultSettings.IsEnabled) return;

        services.AddSingleton(settings.DefaultSettings);
        services.AddSingleton<IEventConsumerServiceCreator, EventConsumerServiceCreator>();
        services.AddSingleton<IRabbitMqConnectionCreator, RabbitMqConnectionCreator>();

        AddOrUpdateVirtualHostSettings(settings, virtualHostSettingsOptions);

        services.AddSingleton<IEventPublisherManager>(serviceProvider =>
        {
            var publisherManager = new EventPublisherManager(serviceProvider);

            var publishers = settings.Publishers ?? new Dictionary<string, EventPublisherOptions>();
            var allPublisherTypes = GetPublisherTypes(assemblies);
            RegisterAllPublishers(publisherManager, allPublisherTypes, publishers);

            var publisherManagerOptions = new EventPublisherManagerOptions(publisherManager);
            eventPublisherManagerOptions?.Invoke(publisherManagerOptions);

            publisherManager.SetVirtualHostAndOwnSettingsOfPublishers(settings.VirtualHostSettings);

            return publisherManager;
        });

        RegisterAllSubscriberReceiversToDependencyInjection(services, assemblies);

        services.AddSingleton<IEventSubscriberManager>(serviceProvider =>
        {
            var subscriberManager = new EventSubscriberManager(settings.DefaultSettings, serviceProvider);

            var subscribers = settings.Subscribers ?? new Dictionary<string, EventSubscriberOptions>();
            RegisterAllSubscribers(subscriberManager, assemblies, subscribers);

            var subscriberManagerOptions = new EventSubscriberManagerOptions(subscriberManager);
            eventSubscriberManagerOptions?.Invoke(subscriberManagerOptions);

            subscriberManager.SetVirtualHostAndOwnSettingsOfSubscribers(settings.VirtualHostSettings);

            return subscriberManager;
        });

        services.AddHostedService<StartEventBusServices>();
    }

    #region Settings of virtual host

    /// <summary>
    /// For adding or updating virtual host settings which is coming from the configuration with the new settings. The set the not assigned setting for each a virtual host settings from the default settings.
    /// </summary>
    /// <param name="settings">The main RabbitMQ settings structure to collect all settings</param>
    /// <param name="virtualHostSettingsOptions">Virtual host settings options to add or update the settings of configuration</param>
    private static void AddOrUpdateVirtualHostSettings(RabbitMqSettings settings,
        Action<Dictionary<string, RabbitMqHostSettings>> virtualHostSettingsOptions)
    {
        Dictionary<string, RabbitMqHostSettings> virtualHostSettings = new();
        virtualHostSettingsOptions?.Invoke(virtualHostSettings);

        foreach (var (virtualHostKey, value) in virtualHostSettings)
        {
            if (settings.VirtualHostSettings.TryGetValue(virtualHostKey, out var settingsValue))
                settingsValue.CopyNotAssignedSettingsFrom(value);
            else
                settings.VirtualHostSettings[virtualHostKey] = value;
        }

        foreach (var hostSettings in settings.VirtualHostSettings.Values)
            hostSettings.CopyNotAssignedSettingsFrom(settings.DefaultSettings);
    }

    #endregion

    #region Publishers

    static readonly Type PublisherType = typeof(IPublishEvent);

    internal static Type[] GetPublisherTypes(Assembly[] assemblies)
    {
        if (assemblies is not null)
        {
            return assemblies.SelectMany(a => a.GetTypes())
                .Where(t => t is { IsClass: true, IsAbstract: false } && PublisherType.IsAssignableFrom(t)).ToArray();
        }

        return [];
    }

    private static void RegisterAllPublishers(EventPublisherManager publisherManager,
        Type[] publisherTypes, Dictionary<string, EventPublisherOptions> publishersOptions)
    {
        foreach (var typeOfPublisher in publisherTypes)
        {
            if (publishersOptions.TryGetValue(typeOfPublisher.Name, out var settings))
                publisherManager.AddPublisher(typeOfPublisher, settings);
            else
                publisherManager.AddPublisher(typeOfPublisher, new EventPublisherOptions());
        }
    }

    /// <summary>
    /// It will load the default settings of RabbitMQ from the configuration. If it is not set, it will use the default settings, otherwise it will set not assigned settings from the default settings.
    /// </summary>
    /// <param name="settingsFromConfig">Main settings from configuration</param>
    /// <param name="defaultOptions">Settings option to overwrite the default settings</param>
    private static void LoadDefaultRabbitMqOptions(RabbitMqSettings settingsFromConfig, Action<RabbitMqOptions> defaultOptions)
    {
        var defaultSettings = RabbitMqOptionsConstant.CreateDefaultRabbitMqOptions();
        if (settingsFromConfig.DefaultSettings is null)
            settingsFromConfig.DefaultSettings = defaultSettings;
        else
            settingsFromConfig.DefaultSettings.CopyNotAssignedSettingsFrom(defaultSettings);
        
        defaultOptions?.Invoke(settingsFromConfig.DefaultSettings);
    }

    #endregion

    #region Subscribers

    private static void RegisterAllSubscribers(EventSubscriberManager subscriberManager,
        Assembly[] assemblies, Dictionary<string, EventSubscriberOptions> subscribersOptions)
    {
        var subscriberReceiverTypes = GetSubscriberReceiverTypes(assemblies);

        foreach (var (eventType, handlerType) in subscriberReceiverTypes)
        {
            if (subscribersOptions.TryGetValue(eventType.Name, out var settings))
                subscriberManager.AddSubscriber(eventType, handlerType, settings);
            else
                subscriberManager.AddSubscriber(eventType, handlerType, new EventSubscriberOptions());
        }
    }

    private static void RegisterAllSubscriberReceiversToDependencyInjection(IServiceCollection services, Assembly[] assemblies)
    {
        var subscriberReceiverTypes = GetSubscriberReceiverTypes(assemblies);
        foreach (var (_, handlerType) in subscriberReceiverTypes)
            services.AddTransient(handlerType);
    }
    
    static readonly Type SubscriberReceiverType = typeof(IEventSubscriber<>);

    internal static List<(Type eventType, Type handlerType)> GetSubscriberReceiverTypes(Assembly[] assemblies)
    {
        List<(Type eventType, Type handlerType)> subscriberHandlerTypes = new();
        if (assemblies is not null)
        {
            var allTypes = assemblies
                .SelectMany(a => a.GetTypes())
                .Where(t => t is { IsClass: true, IsAbstract: false });
            foreach (var type in allTypes)
            {
                foreach (var implementedInterface in type.GetInterfaces())
                {
                    if (implementedInterface.IsGenericType &&
                        implementedInterface.GetGenericTypeDefinition() == SubscriberReceiverType)
                    {
                        var eventType = implementedInterface.GetGenericArguments().Single();
                        subscriberHandlerTypes.Add((eventType, type));
                        break;
                    }
                }
            }
        }

        return subscriberHandlerTypes;
    }

    #endregion
}