using DeliveryService.Messaging.Events.Publishers;
using DeliveryService.Messaging.Events.Subscribers;
using EventBus.RabbitMQ.Subscribers.Models;
using EventStorage.Outbox.Managers;

namespace DeliveryService.Messaging.Subscribers;

public class DeliverOrderSubscriber(
    IOutboxEventManager outboxEventManager) :
    IEventSubscriber<DeliverOrder>
{
    public async Task HandleAsync(DeliverOrder @event)
    {
        // 90% success rate
        if (Random.Shared.Next(100) < 90)
        {
            await outboxEventManager.StoreAsync(new OrderDelivered
            {
                OrderId = @event.OrderId
            });
        }
        else
        {
            await outboxEventManager.StoreAsync(new OrderFailed
            {
                OrderId = @event.OrderId,
                Reason = "Delivery failed"
            });
        }
    }
}