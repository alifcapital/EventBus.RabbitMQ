using EventBus.RabbitMQ.Publishers.Models;
using EventStorage.Models;
using EventStorage.Outbox.Managers;
using Microsoft.Extensions.Logging;

namespace EventBus.RabbitMQ.Publishers.Managers;

/// <summary>
/// The implementation of <see cref="IEventPublisherManager"/> to use when outbox is enabled.
/// </summary>
internal class OutboxEventPublisherManager(IOutboxEventManager outboxEventManager) 
    : IEventPublisherManager
{
    #region PublishAsync

    public async Task PublishAsync<TPublishEvent>(TPublishEvent publishEvent, CancellationToken cancellationToken)
        where TPublishEvent : class, IPublishEvent
    {
        await outboxEventManager.StoreAsync(publishEvent, EventProviderType.MessageBroker);
    }

    #endregion

    #region Collect

    public void Collect<TPublishEvent>(TPublishEvent publishEvent) where TPublishEvent : class, IPublishEvent
    {
        outboxEventManager.Collect(publishEvent, EventProviderType.MessageBroker);
    }

    #endregion

    #region CleanCollectedEvents

    public void CleanCollectedEvents()
    {
        outboxEventManager.CleanCollectedEvents();
    }

    #endregion

    #region Dispose

    private bool _disposed;

    public void Dispose()
    {
        Disposing();
        GC.SuppressFinalize(this);
    }

    private void Disposing()
    {
        if (_disposed) return;

        _disposed = true;
    }

    ~OutboxEventPublisherManager()
    {
        Disposing();
    }

    #endregion
}