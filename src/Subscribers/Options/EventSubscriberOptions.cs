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

        QueueNameIfEmpty();
    }

    /// <summary>
    /// If the queue name is empty, it will use the queue name from the virtual host settings. If that also is empty, then use the exchange name as default value.
    /// </summary>
    private void QueueNameIfEmpty()
    {
        if (string.IsNullOrEmpty(QueueName))
            QueueName = string.IsNullOrEmpty(VirtualHostSettings.QueueName) ? VirtualHostSettings.ExchangeName : VirtualHostSettings.QueueName;
    }
}