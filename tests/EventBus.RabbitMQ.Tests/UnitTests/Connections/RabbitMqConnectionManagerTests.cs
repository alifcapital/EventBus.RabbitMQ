using System.Reflection;
using EventBus.RabbitMQ.Configurations;
using EventBus.RabbitMQ.Connections;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace EventBus.RabbitMQ.Tests.UnitTests.Connections;

public class RabbitMqConnectionManagerTests : BaseTestEntity
{
    private RabbitMqConnectionManager _connectionManager;

    #region SetUp

    [SetUp]
    public void Setup()
    {
        var serviceProvider = Substitute.For<IServiceProvider>();
        var logger = Substitute.For<ILogger<RabbitMqConnection>>();
        serviceProvider.GetService(typeof(ILogger<RabbitMqConnection>)).Returns(logger);
        _connectionManager = new RabbitMqConnectionManager(serviceProvider);
    }

    #endregion
    
    [Test]
    public void GetOrCreateConnection_CreatingConnectionOnce_ShouldCreateSingleConnectionAndCacheIt()
    {
        var rabbitMqOptions = RabbitMqOptionsConstant.CreateDefaultRabbitMqOptions();

        var connection = _connectionManager.GetOrCreateConnection(rabbitMqOptions);
        
        Assert.That(connection, Is.Not.Null);
        var connections = GetRabbitMqConnections();
        Assert.That(connections!.Count, Is.EqualTo(1));
    }
    
    [Test]
    public void GetOrCreateConnection_CreatingConnectionTwiceWithSingleSettings_ShouldCreateSingleConnectionAndCacheIt()
    {
        var virtualHostSettings = RabbitMqOptionsConstant.CreateDefaultRabbitMqOptions();

        var connection1 = _connectionManager.GetOrCreateConnection(virtualHostSettings);
        var connection2 = _connectionManager.GetOrCreateConnection(virtualHostSettings);
        
        Assert.That(connection1, Is.EqualTo(connection2));
        var connections = GetRabbitMqConnections();
        Assert.That(connections!.Count, Is.EqualTo(1));
    }
    
    [Test]
    public void GetOrCreateConnection_CreatingTwoConnectionsWithDifferentSettings_ShouldCreateSingleConnectionAndCacheIt()
    {
        var virtualHostSettings1 = RabbitMqOptionsConstant.CreateDefaultRabbitMqOptions();
        var virtualHostSettings2 = RabbitMqOptionsConstant.CreateDefaultRabbitMqOptions();
        virtualHostSettings2.VirtualHost = "vhost2";

        var connection1 = _connectionManager.GetOrCreateConnection(virtualHostSettings1);
        var connection2 = _connectionManager.GetOrCreateConnection(virtualHostSettings2);
        
        Assert.That(connection1, Is.Not.EqualTo(connection2));
        var connections = GetRabbitMqConnections();
        Assert.That(connections!.Count, Is.EqualTo(2));
    }

    #region Helper methods

    /// <summary>
    /// Gets the cached connections using reflection.
    /// </summary>
    private Dictionary<string, RabbitMqConnection> GetRabbitMqConnections()
    {
        var field = _connectionManager.GetType()
            .GetField("_connections", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null);
        var connections = field!.GetValue(_connectionManager) as Dictionary<string, RabbitMqConnection>;
        return connections;
    }

    #endregion
}