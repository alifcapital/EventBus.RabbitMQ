using EventStorage.Inbox.Providers;

namespace EventBus.RabbitMQ.Subscribers.Models;

/// <summary>
/// The base interface for all event subscriber handler classes.
/// </summary>
public interface IEventSubscriber<in TSubscribeEvent> : IMessageBrokerEventHandler<TSubscribeEvent>
    where TSubscribeEvent : class, ISubscribeEvent;