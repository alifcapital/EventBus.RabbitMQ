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
    
    [Test]
    public void AddSubscriberIfNotExists_AddingOneSubscriberInformationTwice_ShouldBeRegisteredSingleSubscriber()
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
        subscribersInformation.AddSubscriberIfNotExists(eventType, subscriberType);
        
        Assert.That(subscribersInformation.Subscribers.Count, Is.EqualTo(1));
    }
    
    [Test]
    public void AddSubscriberIfNotExists_AddingOneEventWithTwoSubscribers_ShouldBeRegisteredTwoSubscribers()
    {
        var typeOfEvent1 = typeof(Domain.Module1.UserCreated);
        var typeOfEventSubscriber1 = typeof(Domain.Module1.UserCreatedSubscriber);
        var typeOfEventSubscriber2 = typeof(Domain.Module2.UserCreatedSubscriber);
        var settings = new EventSubscriberOptions();
        var subscribersInfo = new SubscribersInformation
        {
            EventTypeName = typeOfEvent1.Name,
            Settings = settings
        };
        subscribersInfo.AddSubscriberIfNotExists(typeOfEvent1, typeOfEventSubscriber1);
        subscribersInfo.AddSubscriberIfNotExists(typeOfEvent1, typeOfEventSubscriber2);
        
        Assert.That(subscribersInfo.Subscribers.Count, Is.EqualTo(2));
        Assert.That(subscribersInfo.Subscribers.Any(r => r.EventType == typeOfEvent1 && r.EventSubscriberType == typeOfEventSubscriber1), Is.True);
        Assert.That(subscribersInfo.Subscribers.Any(r => r.EventType == typeOfEvent1 && r.EventSubscriberType == typeOfEventSubscriber2), Is.True);
    }
    
    [Test]
    public void AddSubscriberIfNotExists_AddingTwoEventsWithSubscribers_ShouldBeRegisteredTwoSubscribers()
    {
        var typeOfEvent1 = typeof(Domain.Module1.UserCreated);
        var typeOfEvent2 = typeof(Domain.Module2.UserCreated);
        var typeOfEventSubscriber1 = typeof(Domain.Module1.UserCreatedSubscriber);
        var typeOfEventSubscriber2 = typeof(Domain.Module2.UserCreatedSubscriber);
        var settings = new EventSubscriberOptions();
        var subscribersInfo = new SubscribersInformation
        {
            EventTypeName = typeOfEvent1.Name,
            Settings = settings
        };
        subscribersInfo.AddSubscriberIfNotExists(typeOfEvent1, typeOfEventSubscriber1);
        subscribersInfo.AddSubscriberIfNotExists(typeOfEvent2, typeOfEventSubscriber2);
        
        Assert.That(subscribersInfo.Subscribers.Count, Is.EqualTo(2));
        Assert.That(subscribersInfo.Subscribers.Any(r => r.EventType == typeOfEvent1 && r.EventSubscriberType == typeOfEventSubscriber1), Is.True);
        Assert.That(subscribersInfo.Subscribers.Any(r => r.EventType == typeOfEvent2 && r.EventSubscriberType == typeOfEventSubscriber2), Is.True);
    }
}