using EventBus.RabbitMQ.Subscribers.Options;

namespace EventBus.RabbitMQ.Subscribers.Models;

internal record SubscribersInformation
{
    private const string HandleMethodName = nameof(IEventSubscriber<ISubscribeEvent>.HandleAsync);

    /// <summary>
    /// The name of the event type.
    /// </summary>
    public required string EventTypeName { get; init; }

    /// <summary>
    /// The settings of the event subscriber.
    /// </summary>
    public required EventSubscriberOptions Settings { get; set; }

    private readonly List<SubscriberInformation> _subscribers = [];

    /// <summary>
    /// All subscribers information which are related to the one event.
    /// </summary>
    public IReadOnlyList<SubscriberInformation> Subscribers => _subscribers.AsReadOnly();

    /// <summary>
    /// Adds a subscriber if it does not exist.
    /// </summary>
    /// <param name="eventType">The type of the event.</param>
    /// <param name="eventHandlerType">The type of the event handler.</param>
    public void AddSubscriberIfNotExists(Type eventType, Type eventHandlerType)
    {
        if (_subscribers.Any(x => x.EventType == eventType && x.EventSubscriberType == eventHandlerType))
            return;

        var handlerMethod = eventHandlerType.GetMethod(HandleMethodName);
        _subscribers.Add(new SubscriberInformation
        {
            EventType = eventType,
            EventSubscriberType = eventHandlerType,
            HandleMethod = handlerMethod
        });
    }
}