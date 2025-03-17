
// namespace UsersService.Messaging.Publishers.Providers;

// /// <summary>
// /// Note: WE DO NOT need to implement this class as it is already implemented in the EventBus.RabbitMQ library. It is just a template.
// /// </summary>
// public class MessageBrokerEventPublisher(IEventPublisherManager eventPublisher) : IMessageBrokerEventPublisher
// {
//     public async Task PublishAsync(IOutboxEvent outboxEvent)
//     {
//         eventPublisher.Publish((IPublishEvent)outboxEvent);
//         
//         await Task.CompletedTask;
//     }
// }