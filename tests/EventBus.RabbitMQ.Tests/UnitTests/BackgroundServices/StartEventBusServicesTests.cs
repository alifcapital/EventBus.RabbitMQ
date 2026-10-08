using System.Reflection;
using System.Runtime.ExceptionServices;
using EventBus.RabbitMQ.BackgroundServices;
using EventBus.RabbitMQ.Publishers.Managers;
using EventBus.RabbitMQ.Subscribers.Managers;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace EventBus.RabbitMQ.Tests.UnitTests.BackgroundServices;

public class StartEventBusServicesTests : BaseTestEntity
{
    private IEventSubscriberCollector _subscriberCollector;
    private IEventPublisherCollector _publisherCollector;
    private ILogger<StartEventBusServices> _logger;
    private StartEventBusServices _service;

    #region SetUp and TearDown

    [SetUp]
    public void Setup()
    {
        _subscriberCollector = Substitute.For<IEventSubscriberCollector>();
        _publisherCollector = Substitute.For<IEventPublisherCollector>();
        _logger = Substitute.For<ILogger<StartEventBusServices>>();
        _service = new StartEventBusServices(_subscriberCollector, _publisherCollector, _logger);
    }

    [TearDown]
    public void TearDown()
    {
        _service.Dispose();
    }

    #endregion

    #region ExecuteAsync

    [Test]
    public async Task ExecuteAsync_WhenStartingFailsWhileStopping_ShouldNotLogError()
    {
        _publisherCollector.CreateExchangeForPublishersAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new ObjectDisposedException(nameof(IServiceProvider))));

        await InvokeExecuteAsync(new CancellationToken(canceled: true));

        Assert.That(HasLog(LogLevel.Error), Is.False);
    }

    [Test]
    public async Task ExecuteAsync_WhenStartingFailsWithoutStopping_ShouldLogError()
    {
        _publisherCollector.CreateExchangeForPublishersAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Broker is unreachable")));

        await InvokeExecuteAsync(CancellationToken.None);

        Assert.That(HasLog(LogLevel.Error), Is.True);
    }

    #endregion

    #region StopAsync

    [Test]
    public async Task StopAsync_WhenApplicationIsStopping_ShouldStopReceivingEventsOfAllConsumers()
    {
        var cancellationToken = CancellationToken.None;

        await _service.StopAsync(cancellationToken);

        await _subscriberCollector.Received(1).StopReceivingEventsAsync(cancellationToken);
    }

    [Test]
    public void StopAsync_WhenStoppingConsumersFails_ShouldNotThrow()
    {
        _subscriberCollector.StopReceivingEventsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Channel is broken")));

        Assert.DoesNotThrowAsync(() => _service.StopAsync(CancellationToken.None));
        Assert.That(HasLog(LogLevel.Warning), Is.True);
    }

    #endregion

    #region Helpers

    private async Task InvokeExecuteAsync(CancellationToken stoppingToken)
    {
        var method = typeof(StartEventBusServices).GetMethod("ExecuteAsync",
            BindingFlags.NonPublic | BindingFlags.Instance);
        try
        {
            await (Task)method!.Invoke(_service, [stoppingToken])!;
        }
        catch (TargetInvocationException ex)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException!).Throw();
        }
    }

    private bool HasLog(LogLevel level)
    {
        return _logger.ReceivedCalls().Any(call =>
            call.GetMethodInfo().Name == nameof(ILogger.Log) &&
            call.GetArguments()[0] is LogLevel logLevel &&
            logLevel == level);
    }

    #endregion
}
