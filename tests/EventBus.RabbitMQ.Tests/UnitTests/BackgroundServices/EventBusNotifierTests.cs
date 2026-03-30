using System.Reflection;
using System.Runtime.ExceptionServices;
using EventBus.RabbitMQ.BackgroundServices;
using EventBus.RabbitMQ.Configurations;
using EventBus.RabbitMQ.Exceptions;
using EventStorage.Configurations;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace EventBus.RabbitMQ.Tests.UnitTests.BackgroundServices;

public class EventBusNotifierTests : BaseTestEntity
{
    private ILogger<EventBusNotifier> _logger;

    #region SetUp

    [SetUp]
    public void Setup()
    {
        _logger = Substitute.For<ILogger<EventBusNotifier>>();
    }

    #endregion

    #region Helpers

    private static async Task InvokeExecuteAsync(EventBusNotifier notifier)
    {
        var method = typeof(EventBusNotifier).GetMethod("ExecuteAsync",
            BindingFlags.NonPublic | BindingFlags.Instance);
        try
        {
            await (Task)method!.Invoke(notifier, [CancellationToken.None])!;
        }
        catch (TargetInvocationException ex)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException!).Throw();
        }
    }

    #endregion

    #region ExecuteAsync

    [Test]
    public async Task ExecuteAsync_RabbitMqDisabled_ShouldLogInformation()
    {
        var rabbitMqOptions = new RabbitMqOptions { IsEnabled = false };
        var eventStorageOptions = new InboxAndOutboxSettings();
        var notifier = new EventBusNotifier(rabbitMqOptions, eventStorageOptions, _logger);

        await InvokeExecuteAsync(notifier);

        _logger.Received(1).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString().Contains("Since the RabbitMQ functionality is disabled, publishing and subscribing events will be skipped.")),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Test]
    public void ExecuteAsync_UseInboxIsTrueButInboxItselfDisabled_ShouldThrowException()
    {
        var rabbitMqOptions = new RabbitMqOptions { IsEnabled = true, UseInbox = true, UseOutbox = false };
        var eventStorageOptions = new InboxAndOutboxSettings
        {
            Inbox = new InboxOrOutboxStructure { IsEnabled = false }
        };
        var notifier = new EventBusNotifier(rabbitMqOptions, eventStorageOptions, _logger);

        var ex = Assert.ThrowsAsync<EventBusException>(async () => await InvokeExecuteAsync(notifier));
        Assert.That(ex.Message.StartsWith("Using an Inbox functionality is enabled"), Is.True);
    }

    [Test]
    public void ExecuteAsync_UseOutboxIsTrueButOutboxItselfDisabled_ShouldThrowException()
    {
        var rabbitMqOptions = new RabbitMqOptions { IsEnabled = true, UseInbox = false, UseOutbox = true };
        var eventStorageOptions = new InboxAndOutboxSettings
        {
            Outbox = new InboxOrOutboxStructure { IsEnabled = false }
        };
        var notifier = new EventBusNotifier(rabbitMqOptions, eventStorageOptions, _logger);

        var ex = Assert.ThrowsAsync<EventBusException>(async () => await InvokeExecuteAsync(notifier));
        Assert.That(ex.Message.StartsWith("Using an Outbox functionality is enabled"), Is.True);
    }

    [Test]
    public void ExecuteAsync_AllInboxAndOutboxParametersAreDisabled_ShouldNotThrow()
    {
        var rabbitMqOptions = new RabbitMqOptions { IsEnabled = true, UseInbox = false, UseOutbox = false };
        var eventStorageOptions = new InboxAndOutboxSettings
        {
            Inbox = new InboxOrOutboxStructure { IsEnabled = false },
            Outbox = new InboxOrOutboxStructure { IsEnabled = false }
        };
        var notifier = new EventBusNotifier(rabbitMqOptions, eventStorageOptions, _logger);

        Assert.DoesNotThrowAsync(async () => await InvokeExecuteAsync(notifier));
    }

    #endregion
}