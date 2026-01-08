
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
}