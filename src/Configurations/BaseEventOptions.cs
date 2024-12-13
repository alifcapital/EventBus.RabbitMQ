using System.Text.Json;
using System.Text.Json.Serialization;
using EventStorage.Models;

namespace EventBus.RabbitMQ.Configurations;

public abstract class BaseEventOptions
{
    /// <summary>
    /// The name of the event. By default, it will get an event name.
    /// </summary>
    public string EventTypeName { get; set; }

    /// <summary>
    /// The routing key to use for message routing in RabbitMQ. If it is empty, it will use the virtual host settings' RoutingKey, If that also is empty, then use the "{ExchangeName}.{EventTypeName}" as default value.
    /// </summary>
    public string RoutingKey { get; set; }

    /// <summary>
    /// The key of virtual host to find the host settings and use that to connect to the RabbitMQ.
    /// </summary>
    public string VirtualHostKey { get; set; }

    /// <summary>
    /// Naming police for serializing and deserializing properties of Event. Default value is "PascalCase". It can be one of "PascalCase", "CamelCase", "SnakeCaseLower", "SnakeCaseUpper", "KebabCaseLower", or "KebabCaseUpper".
    /// </summary>
    public NamingPolicyType? PropertyNamingPolicy { get; set; }

    /// <summary>
    /// Virtual host settings to connect to the RabbitMQ. It should set while loading application based on a <see cref="VirtualHostKey"/> value.
    /// </summary>
    internal RabbitMqHostSettings VirtualHostSettings { get; private set; }

    /// <summary>
    /// Set the virtual host and other unassigned settings.
    /// </summary>
    /// <param name="settings">Virtual host setting to use as a source</param>
    /// <param name="eventTypeName">Event type name set its value</param>
    internal virtual void SetVirtualHostAndUnassignedSettings(RabbitMqHostSettings settings, string eventTypeName)
    {
        ThrowExceptionIfVirtualHostIsNull(settings);
        ThrowExceptionIfExchangeNameIsNull(settings);

        VirtualHostSettings = settings;
        PropertyNamingPolicy ??= settings.PropertyNamingPolicy;

        SetEventTypeNameIfEmpty(eventTypeName);
        SetRoutingKeyIfEmpty();
    }

    private JsonSerializerOptions _jsonSerializerOptions;

    /// <summary>
    /// Gets JsonSerializerOptions to use on naming police for serializing and deserializing properties of Event 
    /// </summary>
    /// <returns></returns>
    public JsonSerializerOptions GetJsonSerializer()
    {
        if (_jsonSerializerOptions is not null)
            return _jsonSerializerOptions;

        _jsonSerializerOptions = new JsonSerializerOptions
            { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

        switch (PropertyNamingPolicy)
        {
            case NamingPolicyType.CamelCase:
                _jsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                break;
            case NamingPolicyType.SnakeCaseLower:
                _jsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
                break;
            case NamingPolicyType.SnakeCaseUpper:
                _jsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseUpper;
                break;
            case NamingPolicyType.KebabCaseLower:
                _jsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.KebabCaseLower;
                break;
            case NamingPolicyType.KebabCaseUpper:
                _jsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.KebabCaseUpper;
                break;
        }

        return _jsonSerializerOptions;
    }

    #region Helper methods

    /// <summary>
    /// The routing key to use for message routing in RabbitMQ. If it is empty, it will use the virtual host settings' RoutingKey, If that also is empty, then use the "{ExchangeName}.{EventTypeName}" as default value.
    /// </summary>
    private void SetRoutingKeyIfEmpty()
    {
        if (string.IsNullOrEmpty(RoutingKey))
            RoutingKey = string.IsNullOrEmpty(VirtualHostSettings.RoutingKey)
                ? $"{VirtualHostSettings.ExchangeName}.{EventTypeName}"
                : VirtualHostSettings.RoutingKey;
    }

    /// <summary>
    /// If the event type name is not assigned, it will be generated based on the event naming policy and set it.
    /// </summary>
    /// <param name="eventTypeName">The type name of event</param>
    private void SetEventTypeNameIfEmpty(string eventTypeName)
    {
        if (string.IsNullOrEmpty(EventTypeName))
            EventTypeName = VirtualHostSettings.GetCorrectEventNameBasedOnNamingPolicy(eventTypeName);
    }

    private static void ThrowExceptionIfVirtualHostIsNull(RabbitMqHostSettings settings)
    {
        if (string.IsNullOrEmpty(settings.VirtualHost))
            throw new ArgumentNullException(settings.VirtualHost,
                $"The {nameof(settings.VirtualHost)} is required, but it is currently null or empty for the {settings.HostName} host.");
    }

    private static void ThrowExceptionIfExchangeNameIsNull(RabbitMqHostSettings settings)
    {
        if (string.IsNullOrEmpty(settings.ExchangeName))
            throw new ArgumentNullException(settings.ExchangeName,
                $"The {nameof(settings.ExchangeName)} is required, but it is currently null or empty for the {settings.VirtualHost} virtual host.");
    }

    #endregion
}