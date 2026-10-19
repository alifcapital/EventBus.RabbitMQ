namespace EventBus.RabbitMQ.Information.Models;

/// <summary>
/// The information of an event class and of its handler. It is used for both publishers and subscribers: the handler of
/// a publishing event is the class which publishes it, and the handler of a subscribed event is its subscriber.
/// </summary>
public record EventHandlerInformation
{
    /// <summary>
    /// The class name of the event.
    /// </summary>
    public required string EventTypeName { get; init; }

    /// <summary>
    /// The namespace (path) of the class of the event.
    /// </summary>
    public required string EventTypeNamespace { get; init; }

    /// <summary>
    /// The class name of the event handler: the publisher of a publishing event, or the subscriber
    /// (<c>IEventSubscriber&lt;TEvent&gt;</c>) of a subscribed event.
    /// </summary>
    public required string HandlerTypeName { get; init; }

    /// <summary>
    /// The namespace (path) of the class of the event handler.
    /// </summary>
    public required string HandlerTypeNamespace { get; init; }

    #region Create

    /// <summary>
    /// Creates the information from the classes of the event and its handler.
    /// </summary>
    /// <param name="eventType">The class of the event.</param>
    /// <param name="handlerType">The class of the event handler.</param>
    /// <returns>The information of the event and its handler.</returns>
    internal static EventHandlerInformation Create(Type eventType, Type handlerType)
    {
        return new EventHandlerInformation
        {
            EventTypeName = eventType.Name,
            EventTypeNamespace = eventType.Namespace,
            HandlerTypeName = handlerType.Name,
            HandlerTypeNamespace = handlerType.Namespace
        };
    }

    #endregion
}
