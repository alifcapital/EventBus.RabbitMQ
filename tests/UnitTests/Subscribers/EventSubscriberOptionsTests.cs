using EventBus.RabbitMQ.Configurations;
using EventBus.RabbitMQ.Subscribers.Options;
using EventStorage.Models;
using FluentAssertions;

namespace EventBus.RabbitMQ.Tests.UnitTests.Subscribers;

public class EventSubscriberOptionsTests : BaseTestEntity
{
    #region Required options

    [Test]
    public void SetVirtualHostAndUnassignedSettings_VirtualHostIsNull_ShouldThrowException()
    {
        var eventTypeName = "UserCreated";
        var settings = new RabbitMqHostSettings
        {
            HostName = "test-host",
        };
        var eventSettings = new EventSubscriberOptions();

        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                eventSettings.SetVirtualHostAndUnassignedSettings(settings, eventTypeName));

        exception!.Message.Should()
            .Be(
                $"The {nameof(settings.VirtualHost)} is required, but it is currently null or empty for the {settings.HostName} host.");
    }

    [Test]
    public void SetVirtualHostAndUnassignedSettings_ExchangeNameIsNull_ShouldThrowException()
    {
        var eventTypeName = "UserCreated";
        var settings = new RabbitMqHostSettings
        {
            VirtualHost = "test-virtual-host",
        };
        var eventSettings = new EventSubscriberOptions();

        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                eventSettings.SetVirtualHostAndUnassignedSettings(settings, eventTypeName));

        exception!.Message.Should()
            .Be(
                $"The {nameof(settings.ExchangeName)} is required, but it is currently null or empty for the {settings.VirtualHost} virtual host.");
    }

    [Test]
    public void SetVirtualHostAndUnassignedSettings_BothRequiredOptionsAreAssigned_ThereShouldNotBeException()
    {
        var settings = new RabbitMqHostSettings
        {
            VirtualHost = "test-virtual-host",
            ExchangeName = "test-exchange",
        };
        var eventSettings = new EventSubscriberOptions();

        Assert.DoesNotThrow(() => eventSettings.SetVirtualHostAndUnassignedSettings(settings, "test"));
    }

    #endregion

    #region EventTypeName

    [Test]
    public void SetVirtualHostAndUnassignedSettings_EventTypeNameAndNamingPoliceAreNotAssigned_ShouldPassingEventTypeName()
    {
        var eventTypeName = "UserCreated";
        var settings = new RabbitMqHostSettings
        {
            VirtualHost = "test-virtual-host",
            ExchangeName = "test-exchange",
        };
        var eventSettings = new EventSubscriberOptions();

        eventSettings.SetVirtualHostAndUnassignedSettings(settings, eventTypeName);

        eventSettings.EventTypeName.Should().Be(eventTypeName);
    }

    [Test]
    public void SetVirtualHostAndUnassignedSettings_EventTypeNameAssignedByEventOption_EventTypeNameOfEventOptionShouldNotOverwritten()
    {
        var eventTypeName = "UserCreated";
        var settings = new RabbitMqHostSettings
        {
            VirtualHost = "test-virtual-host",
            ExchangeName = "test-exchange",
        };
        var eventSettings = new EventSubscriberOptions
        {
            EventTypeName = "User-Created"
        };

        eventSettings.SetVirtualHostAndUnassignedSettings(settings, eventTypeName);

        eventSettings.EventTypeName.Should().Be(eventSettings.EventTypeName);
    }

    [Test]
    public void SetVirtualHostAndUnassignedSettings_EventTypeNameIsNotAssignedButSettingsIsConfiguredToUseEventNamingPolicy_EventTypeNameOfEventOptionShouldCalculatedBasedOnEventNamingPolicy()
    {
        var eventTypeName = "UserCreated";
        var settings = new RabbitMqHostSettings
        {
            VirtualHost = "test-virtual-host",
            ExchangeName = "test-exchange",
            EventNamingPolicy = NamingPolicyType.SnakeCaseLower
        };
        var eventSettings = new EventSubscriberOptions();

        eventSettings.SetVirtualHostAndUnassignedSettings(settings, eventTypeName);

        eventSettings.EventTypeName.Should().Be("user_created");
    }

    #endregion

    #region RoutingKey

    [Test]
    public void SetVirtualHostAndUnassignedSettings_BothSettingsHasRoutingKey_RoutingKeyOfEventOptionShouldNotOverwritten()
    {
        var eventTypeName = "UserCreated";
        var settings = new RabbitMqHostSettings
        {
            VirtualHost = "test-virtual-host",
            ExchangeName = "test-exchange",
            RoutingKey = "test-routing-key"
        };
        var eventSettings = new EventSubscriberOptions
        {
            RoutingKey = "test-event-routing-key"
        };

        eventSettings.SetVirtualHostAndUnassignedSettings(settings, eventTypeName);

        eventSettings.RoutingKey.Should().Be(eventSettings.RoutingKey);
    }

    [Test]
    public void SetVirtualHostAndUnassignedSettings_OnlyHostSettingsHasRoutingKey_RoutingKeyOfEventOptionShouldBeEqualToRoutingKeyOfSettings()
    {
        var eventTypeName = "UserCreated";
        var settings = new RabbitMqHostSettings
        {
            VirtualHost = "test-virtual-host",
            ExchangeName = "test-exchange",
            RoutingKey = "test-routing-key"
        };
        var eventSettings = new EventSubscriberOptions();

        eventSettings.SetVirtualHostAndUnassignedSettings(settings, eventTypeName);

        eventSettings.RoutingKey.Should().Be(settings.RoutingKey);
    }

    [Test]
    public void SetVirtualHostAndUnassignedSettings_BothSettingsDoesNotHaveRoutingKey_RoutingKeyOfEventOptionShouldBeCalculated()
    {
        var eventTypeName = "UserCreated";
        var settings = new RabbitMqHostSettings
        {
            VirtualHost = "test-virtual-host",
            ExchangeName = "test-exchange"
        };
        var eventSettings = new EventSubscriberOptions();

        eventSettings.SetVirtualHostAndUnassignedSettings(settings, eventTypeName);

        var calculatedRoutingKey = $"{settings.ExchangeName}.{eventTypeName}";
        eventSettings.RoutingKey.Should().Be(calculatedRoutingKey);
    }

    [Test]
    public void SetVirtualHostAndUnassignedSettings_BothSettingsDoesNotHaveRoutingKeyAndEventNamingPolicyConfigured_RoutingKeyOfEventOptionShouldBeCalculatedBasedOnEventNamingPolicy()
    {
        var eventTypeName = "UserCreated";
        var settings = new RabbitMqHostSettings
        {
            VirtualHost = "test-virtual-host",
            ExchangeName = "test-exchange",
            EventNamingPolicy = NamingPolicyType.SnakeCaseLower
        };
        var eventSettings = new EventSubscriberOptions();

        eventSettings.SetVirtualHostAndUnassignedSettings(settings, eventTypeName);

        var calculatedRoutingKey = $"{settings.ExchangeName}.user_created";
        eventSettings.RoutingKey.Should().Be(calculatedRoutingKey);
    }

    #endregion
    
    #region QueueName

    [Test]
    public void SetVirtualHostAndUnassignedSettings_BothSettingsHasQueueName_QueueNameOfEventOptionShouldNotOverwritten()
    {
        var eventTypeName = "UserCreated";
        var settings = new RabbitMqHostSettings
        {
            VirtualHost = "test-virtual-host",
            ExchangeName = "test-exchange",
            QueueName = "test-queue-name"
        };
        var eventSettings = new EventSubscriberOptions
        {
            QueueName = "test-event-queue-name"
        };

        eventSettings.SetVirtualHostAndUnassignedSettings(settings, eventTypeName);

        eventSettings.QueueName.Should().Be(eventSettings.QueueName);
    }

    [Test]
    public void SetVirtualHostAndUnassignedSettings_OnlyHostSettingsHasQueueName_QueueNameOfEventOptionShouldBeEqualToQueueNameOfSettings()
    {
        var eventTypeName = "UserCreated";
        var settings = new RabbitMqHostSettings
        {
            VirtualHost = "test-virtual-host",
            ExchangeName = "test-exchange",
            QueueName = "test-queue-name"
        };
        var eventSettings = new EventSubscriberOptions();

        eventSettings.SetVirtualHostAndUnassignedSettings(settings, eventTypeName);

        eventSettings.QueueName.Should().Be(settings.QueueName);
    }

    [Test]
    public void SetVirtualHostAndUnassignedSettings_BothSettingsDoesNotHaveQueueName_QueueNameOfEventOptionShouldBeComputedFromExchangeName()
    {
        var eventTypeName = "UserCreated";
        var settings = new RabbitMqHostSettings
        {
            VirtualHost = "test-virtual-host",
            ExchangeName = "test-exchange"
        };
        var eventSettings = new EventSubscriberOptions();
    
        eventSettings.SetVirtualHostAndUnassignedSettings(settings, eventTypeName);
    
        eventSettings.QueueName.Should().Be(settings.ExchangeName);
    }

    #endregion
}