using EventBus.RabbitMQ.Configurations;

namespace EventBus.RabbitMQ.Subscribers.Options;

public class EventSubscriberOptions : BaseEventOptions
{
    /// <summary>
    /// The name of the queue to use in RabbitMQ. If it is empty, it will use the virtual host settings' queue name, If that also is empty, then use the exchange name as default value.
    /// </summary>
    public string QueueName { get; set; }

    internal override void SetVirtualHostAndUnassignedSettings(RabbitMqHostSettings settings, string eventTypeName)
    {
        base.SetVirtualHostAndUnassignedSettings(settings, eventTypeName);
        
        if (string.IsNullOrEmpty(QueueName))
            QueueName = string.IsNullOrEmpty(settings.QueueName) ? settings.ExchangeName : settings.QueueName;
    }
}