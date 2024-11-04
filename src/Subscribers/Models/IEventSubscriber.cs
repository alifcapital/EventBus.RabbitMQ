using EventStorage.Inbox.Providers;

namespace EventBus.RabbitMQ.Subscribers.Models;

/// <summary>
/// Base interface for all event subscriber handler classes
/// </summary>
public interface IEventSubscriber<TEventSubscriber> : IMessageBrokerEventReceiver<TEventSubscriber>
    where TEventSubscriber : class, ISubscribeEvent
{
}