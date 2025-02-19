using EventBus.RabbitMQ.Subscribers.Models;
using EventBus.RabbitMQ.Subscribers.Options;
using EventBus.RabbitMQ.Tests.Domain;

namespace EventBus.RabbitMQ.Tests.UnitTests.Subscribers;

public class SubscribersInformationTests : BaseTestEntity
{
    [Test]
    public void AddSubscriberIfNotExists_AddingOneSubscriberInformation_ShouldBeRegisteredSingleSubscriber()
    {
        var queueName = "test-queue";
        var eventType = typeof(SimpleSubscribeEvent);
        var subscriberType = typeof(SimpleEventSubscriberHandler);
        var settings = new EventSubscriberOptions
        {
            EventTypeName = eventType.Name,
            QueueName = queueName,
        };
        var subscribersInformation = new SubscribersInformation
        {
            EventTypeName = eventType.Name,
            Settings = settings
        };
        subscribersInformation.AddSubscriberIfNotExists(eventType, subscriberType);
        
        Assert.That(subscribersInformation.Subscribers.Count, Is.EqualTo(1));
        
        var subscriberInfo = subscribersInformation.Subscribers.First();
        Assert.That(subscriberInfo.EventType, Is.EqualTo(eventType));
        Assert.That(subscriberInfo.EventSubscriberType, Is.EqualTo(subscriberType));
    }
    
    //TODO add more tests
}