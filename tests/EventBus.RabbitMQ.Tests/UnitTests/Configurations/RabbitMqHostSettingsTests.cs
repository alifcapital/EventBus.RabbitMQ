using EventBus.RabbitMQ.Configurations;

namespace EventBus.RabbitMQ.Tests.UnitTests.Configurations;

public class RabbitMqHostSettingsTests : BaseTestEntity
{
    #region PublisherConfirmation

    [Test]
    public void CreateDefaultRabbitMqOptions_CreatingDefaultOptions_ShouldEnablePublisherConfirmation()
    {
        var defaultOptions = RabbitMqOptionsConstant.CreateDefaultRabbitMqOptions();

        Assert.That(defaultOptions.PublisherConfirmation, Is.True);
    }

    [Test]
    public void CopyNotAssignedSettingsFrom_WhenVirtualHostDoesNotSetPublisherConfirmation_ShouldInheritEnabledPublisherConfirmation()
    {
        var virtualHostSettings = new RabbitMqHostSettings
        {
            VirtualHost = "TestVirtualHost",
            ExchangeName = "TestExchangeName"
        };

        virtualHostSettings.CopyNotAssignedSettingsFrom(RabbitMqOptionsConstant.CreateDefaultRabbitMqOptions());

        Assert.That(virtualHostSettings.PublisherConfirmation, Is.True);
    }

    [Test]
    public void CopyNotAssignedSettingsFrom_WhenVirtualHostDisablesPublisherConfirmation_ShouldKeepItDisabled()
    {
        var virtualHostSettings = new RabbitMqHostSettings
        {
            VirtualHost = "TestVirtualHost",
            ExchangeName = "TestExchangeName",
            PublisherConfirmation = false
        };

        virtualHostSettings.CopyNotAssignedSettingsFrom(RabbitMqOptionsConstant.CreateDefaultRabbitMqOptions());

        Assert.That(virtualHostSettings.PublisherConfirmation, Is.False);
    }

    #endregion
}