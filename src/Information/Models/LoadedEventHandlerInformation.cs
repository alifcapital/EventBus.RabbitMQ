namespace EventBus.RabbitMQ.Information.Models;

/// <summary>
/// The information of one handler of a subscribed event.
/// </summary>
public record LoadedEventHandlerInformation
{
    /// <summary>
    /// The class of the subscribed event which is handled by the handler.
    /// </summary>
    public required Type EventType { get; init; }

    /// <summary>
    /// The class of the event handler, which implements <c>IEventSubscriber&lt;TEvent&gt;</c>.
    /// </summary>
    public required Type HandlerType { get; init; }
}
