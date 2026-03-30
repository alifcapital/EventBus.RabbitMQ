using EventBus.RabbitMQ.Publishers.Managers;
using EventBus.RabbitMQ.Tests.Domain;
using EventStorage.Models;
using EventStorage.Outbox.Managers;
using NSubstitute;

namespace EventBus.RabbitMQ.Tests.UnitTests.Publishers;

public class OutboxEventPublisherManagerTests : BaseTestEntity
{
    private IOutboxEventManager _outboxEventManager;
    private OutboxEventPublisherManager _publisherManager;

    #region SetUp and TearDown

    [SetUp]
    public void Setup()
    {
        _outboxEventManager = Substitute.For<IOutboxEventManager>();
        _publisherManager = new OutboxEventPublisherManager(_outboxEventManager);
    }

    [TearDown]
    public void TearDown()
    {
        _publisherManager.Dispose();
        _outboxEventManager.Dispose();
    }

    #endregion

    #region PublishAsync

    [Test]
    public async Task PublishAsync_PublishingOneEvent_ShouldCallStoreAsyncOnOutboxEventManager()
    {
        var publishEvent = new SimplePublishEvent();
        var cancellationToken = CancellationToken.None;

        await _publisherManager.PublishAsync(publishEvent, cancellationToken);

        await _outboxEventManager.Received(1).StoreAsync(publishEvent, EventProviderType.MessageBroker);
    }

    [Test]
    public async Task PublishAsync_PublishingTwoEvents_ShouldCallStoreAsyncTwice()
    {
        var firstEvent = new SimplePublishEvent();
        var secondEvent = new SimplePublishEvent();
        var cancellationToken = CancellationToken.None;

        await _publisherManager.PublishAsync(firstEvent, cancellationToken);
        await _publisherManager.PublishAsync(secondEvent, cancellationToken);

        await _outboxEventManager.Received(2).StoreAsync(Arg.Any<SimplePublishEvent>(), EventProviderType.MessageBroker);
    }

    #endregion

    #region Collect

    [Test]
    public void Collect_CollectingOneEvent_ShouldCallOutboxCollect()
    {
        var publishEvent = new SimplePublishEvent();

        _publisherManager.Collect(publishEvent);

        _outboxEventManager.Received(1).Collect(publishEvent, EventProviderType.MessageBroker);
    }

    [Test]
    public void Collect_CollectingTwoEvents_ShouldCallOutboxCollectTwice()
    {
        var firstEvent = new SimplePublishEvent();
        var secondEvent = new SimplePublishEvent();

        _publisherManager.Collect(firstEvent);
        _publisherManager.Collect(secondEvent);

        _outboxEventManager.Received(2).Collect(Arg.Any<SimplePublishEvent>(), EventProviderType.MessageBroker);
    }

    #endregion

    #region CleanCollectedEvents

    [Test]
    public void CleanCollectedEvents_Cleaning_ShouldCallOutboxCleanCollectedEvents()
    {
        _publisherManager.CleanCollectedEvents();

        _outboxEventManager.Received(1).CleanCollectedEvents();
    }

    #endregion

    #region Dispose

    [Test]
    public void Dispose_WhenCalled_ShouldNotCallStoreAsyncOrPublishDirectly()
    {
        _publisherManager.Dispose();

        _outboxEventManager.DidNotReceive().StoreAsync(Arg.Any<SimplePublishEvent>(), Arg.Any<EventProviderType>());
    }

    [Test]
    public void Dispose_WhenCalledTwice_ShouldNotThrow()
    {
        _publisherManager.Dispose();

        Assert.DoesNotThrow(() => _publisherManager.Dispose());
    }

    #endregion
}