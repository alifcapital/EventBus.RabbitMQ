using EventBus.RabbitMQ.Subscribers.Models;

namespace OrdersService.Messaging.Events.Module1
{
    public record UserCreated : SubscribeEvent
    {
        public Guid UserId { get; init; }

        public string UserName { get; init; }

        public string Age { get; init; }
    }
}

namespace OrdersService.Messaging.Events.Module2
{
    public record UserCreated : SubscribeEvent
    {
        public Guid UserId { get; init; }

        public string UserName { get; init; }

        public string ParentName { get; init; }
    }
}
