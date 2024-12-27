namespace EventBus.RabbitMQ.Subscribers.Models;

public class SubscribedMessageBrokerEventArgs : EventArgs
{
    /// <summary>
    /// Executing received event.
    /// </summary>
    public ISubscribeEvent Event { get; }
    
    /// <summary>
    /// The name of system that the event published by it.
    /// </summary>
    public string SystemName { get; }
    
    /// <summary>
    /// The <see cref="IServiceProvider"/> used to resolve dependencies from the scope.
    /// </summary>
    public IServiceProvider ServiceProvider { get; }
    
    public SubscribedMessageBrokerEventArgs(ISubscribeEvent @event, string systemName, IServiceProvider serviceProvider)
    {
        Event = @event;
        SystemName = systemName;
        ServiceProvider = serviceProvider;
    }
}