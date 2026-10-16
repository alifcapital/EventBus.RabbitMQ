namespace EventBus.RabbitMQ.Information.Models;

/// <summary>
/// The information of the class of an event. It is used for showing both publishers and subscribers information.
/// </summary>
public record EventTypeInformation
{
    /// <summary>
    /// The class name of the event.
    /// </summary>
    public required string EventTypeName { get; init; }

    /// <summary>
    /// The namespace (path) of the class of the event.
    /// </summary>
    public required string EventTypeNamespace { get; init; }

    #region Create

    /// <summary>
    /// Creates the information from the class of the event.
    /// </summary>
    /// <param name="eventType">The class of the event.</param>
    /// <returns>The information of the class of the event.</returns>
    internal static EventTypeInformation Create(Type eventType)
    {
        return new EventTypeInformation
        {
            EventTypeName = eventType.Name,
            EventTypeNamespace = eventType.Namespace
        };
    }

    #endregion
}
