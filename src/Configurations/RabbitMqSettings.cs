using EventBus.RabbitMQ.Publishers.Options;
using EventBus.RabbitMQ.Subscribers.Options;

namespace EventBus.RabbitMQ.Configurations;

public class RabbitMqSettings
{
    /// <summary>
    /// The default settings for connecting to the RabbitMQ server. If a publisher or subscriber does not have specific settings, these default settings will be used.
    /// </summary>
    public RabbitMqOptions DefaultSettings { get; set; }

    /// <summary>
    /// A dictionary where each key represents a publisher and its associated options for connecting to the RabbitMQ server and publishing messages. If no specific settings are provided, it will use the default options.
    /// </summary>
    public Dictionary<string, EventPublisherOptions> Publishers { get; init; } = new();

    /// <summary>
    /// A dictionary where each key represents a subscriber and its associated options for connecting to the RabbitMQ server and receiving messages. If no specific settings are provided, it will use the default options.
    /// </summary>
    public Dictionary<string, EventSubscriberOptions> Subscribers { get; init; } = new();

    /// <summary>
    /// A dictionary where each key represents a virtual host configuration settings for connecting to the RabbitMQ server and publishing messages. If no specific settings are provided, it will add and use the default virtual host settings as default key.
    /// </summary>
    public Dictionary<string, RabbitMqHostSettings> VirtualHostSettings { get; init; } = new();
}