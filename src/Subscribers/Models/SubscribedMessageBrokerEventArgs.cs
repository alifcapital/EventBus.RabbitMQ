namespace EventBus.RabbitMQ.Subscribers.Models;

public class SubscribedMessageBrokerEventArgs : EventArgs
{
    /// <summary>
    /// Executing received event.
    /// </summary>
    public required ISubscribeEvent Event { get; init; }
    
    /// <summary>
    /// The name of system that the event published by it.
    /// </summary>
    public required string SystemName { get; init; }
}