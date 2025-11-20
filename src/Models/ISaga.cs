namespace EventBus.RabbitMQ.Models;

/// <summary>
/// The interface that defines a Saga event.
/// </summary>
public interface ISaga
{
    /// <summary>
    /// The correlation ID to trace the event flow across different services.
    /// </summary>
    Guid CorrelationId { get; init; }
}