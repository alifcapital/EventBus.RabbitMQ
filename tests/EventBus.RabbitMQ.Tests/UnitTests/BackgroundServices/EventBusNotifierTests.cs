using EventBus.RabbitMQ.BackgroundServices;
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

}