
namespace EventBus.RabbitMQ.Configurations;

public class RabbitMqOptions : RabbitMqHostSettings
{
    /// <summary>
    /// To enable using an RabbitMQ. Default value is "true".
    /// </summary>
    public bool IsEnabled { get; init; } = true;
    
    /// <summary>
    /// To enable using an inbox for storing all received events before handling. Default value is "false".
    /// </summary>
    public bool UseInbox { get; init; } = false;

    /// <summary>
    /// To enable using an outbox for storing all publishing events before sending to RabbitMQ. Default value is "false".
    /// </summary>
    public bool UseOutbox { get; init; } = false;
}