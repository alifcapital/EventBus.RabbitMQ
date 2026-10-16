namespace EventBus.RabbitMQ.Information.Models;

/// <summary>
/// The information of one handler of a subscribed event, including the information of the event class it handles.
/// </summary>
public record EventHandlerInformation : EventTypeInformation
{
    /// <summary>
    /// The class name of the event handler, which implements <c>IEventSubscriber&lt;TEvent&gt;</c>.
    /// </summary>
    public required string HandlerTypeName { get; init; }

    /// <summary>
    /// The namespace (path) of the class of the event handler.
    /// </summary>
    public required string HandlerTypeNamespace { get; init; }

    #region Create

    /// <summary>
    /// Creates the information from the classes of the subscribed event and its handler.
    /// </summary>
    /// <param name="eventType">The class of the subscribed event.</param>
    /// <param name="handlerType">The class of the event handler.</param>
    /// <returns>The information of the handler.</returns>
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
