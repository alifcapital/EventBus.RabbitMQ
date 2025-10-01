using System.Reflection;
using System.Security.Authentication;
using System.Text.Json;
using EventStorage.Models;

namespace EventBus.RabbitMQ.Configurations;

/// <summary>
/// Structure to hold the settings for connecting to the RabbitMQ server's specific a virtual host.
/// </summary>
public class RabbitMqHostSettings
{
    /// <summary>
    /// The host name of the RabbitMQ server. Default value is "localhost".
    /// </summary>
    public string HostName { get; set; } = null;

    /// <summary>
    /// The port number on which RabbitMQ is running. Default value is "5672".
    /// </summary>
    public int? HostPort { get; set; } = null;

    /// <summary>
    /// The virtual host of the RabbitMQ server. Default value is "/".
    /// </summary>
    public string VirtualHost { get; set; } = null;

    /// <summary>
    /// The username to connect to RabbitMQ. Default value is "guest".
    /// </summary>
    public string UserName { get; set; } = null;

    /// <summary>
    /// The password for the specified username to connect to RabbitMQ. Default value is "guest".
    /// </summary>
    public string Password { get; set; } = null;

    /// <summary>
    /// The name of the exchange to use in RabbitMQ. Default value is "DefaultExchange".
    /// </summary>
    public string ExchangeName { get; set; } = null;

    /// <summary>
    /// The type of the exchange to use in RabbitMQ. Default value is "topic". It can be one of "direct", "fanout", or "topic".
    /// </summary>
    public string ExchangeType { get; set; } = null;

    /// <summary>
    /// The name of the queue to use in RabbitMQ.
    /// </summary>
    public string QueueName { get; set; } = null;

    /// <summary>
    /// The routing key to use for message routing in RabbitMQ.
    /// </summary>
    public string RoutingKey { get; set; } = null;

    /// <summary>
    /// Optional queue arguments, also known as "x-arguments" because of their field name in the AMQP 0-9-1 protocol, is a map (dictionary) of arbitrary key/value pairs that can be provided by clients when a queue is declared.
    /// </summary>
    public Dictionary<string, object> QueueArguments { get; set; } = new();

    /// <summary>
    /// Optional exchange arguments to use when declaring an exchange in RabbitMQ. These arguments can be used to set some properties of the exchange, such as "alternate exchanges", or other custom settings.
    /// </summary>
    public Dictionary<string, object> ExchangeArguments { get; set; } = new();

    /// <summary>
    /// Retry count to connect to the RabbitMQ. Default value is "3".
    /// </summary>
    public int RetryConnectionCount { get; set; }

    /// <summary>
    /// Naming police for serializing and deserializing properties of Event. Default value is "PascalCase". It can be one of "PascalCase", "CamelCase", "SnakeCaseLower", "SnakeCaseUpper", "KebabCaseLower", or "KebabCaseUpper".
    /// </summary>
    public NamingPolicyType? PropertyNamingPolicy { get; init; }

    /// <summary>
    /// Naming police for serializing and deserializing the name of Event. Default value is "PascalCase". It can be one of "PascalCase", "CamelCase", "SnakeCaseLower", "SnakeCaseUpper", "KebabCaseLower", or "KebabCaseUpper".
    /// </summary>
    public NamingPolicyType? EventNamingPolicy { get; init; } 

    /// <summary>
    /// Indicates whether TLS/SSL should be used for the connection. When set to true, the connection will be secured using TLS/SSL.
    /// </summary>
    public bool? UseTls { get; set; }
    
    /// <summary>
    /// Retrieve or set the TLS protocol version. The default value is "Tls".
    /// </summary>
    /// <seealso cref="T:System.Security.Authentication.SslProtocols" />
    public SslProtocols? SslProtocolVersion { get; set; }

    /// <summary>
    /// The file path to the client's certificate used for TLS/SSL authentication. This certificate is typically used to prove the identity of the client to the server.
    /// </summary>
    public string ClientCertPath { get; set; } = null;

    /// <summary>
    /// The file path to the client's private key associated with the client certificate. This key is used to establish a secure TLS/SSL connection and must correspond to the certificate specified in <see cref="ClientCertPath"/>.
    /// </summary>
    public string ClientKeyPath { get; set; } = null;

    /// <summary>
    /// All properties of RabbitMqHostSettings class to use for overwriting the settings
    /// </summary>
    private static readonly PropertyInfo[] PropertiesOfType = typeof(RabbitMqHostSettings)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(p => p.PropertyType is not { IsGenericType: true, IsValueType: false }).ToArray();

    /// <summary>
    /// Overwriting/copying all not assigned settings from passing settings
    /// </summary>
    /// <param name="settings">Settings to use for overwriting the main settings if the settings parameter value is not null</param>
    internal void CopyNotAssignedSettingsFrom(RabbitMqHostSettings settings)
    {
        if (settings is null) return;

        foreach (var property in PropertiesOfType)
        {
            var currentValue = property.GetValue(this);
            if (currentValue is not null)
                continue;

            var value = settings[property.Name];
            if (value is not null)
                property.SetValue(this, value);
        }

        TryMergeDictionaries(settings.QueueArguments, QueueArguments);
        TryMergeDictionaries(settings.ExchangeArguments, ExchangeArguments);
    }

    private object this[string propertyName]
    {
        get
        {
            var propertyInfo = PropertiesOfType.FirstOrDefault(p => p.Name == propertyName);
            return propertyInfo?.GetValue(this);
        }
    }
    
    /// <summary>
    /// Gets the correct event name based on naming policy
    /// </summary>
    /// <param name="eventName">The event name to convert</param>
    /// <returns>Converted name to use it for publishing or receiving event</returns>
    internal string GetCorrectEventNameBasedOnNamingPolicy(string eventName)
    {
        if (string.IsNullOrEmpty(eventName))
            return eventName;

        var eventNamingPolicy = GetEventNamingPolicy();
        if (eventNamingPolicy is null)
            return eventName;

        return eventNamingPolicy.ConvertName(eventName);
    }

    #region Helper methods

    private JsonNamingPolicy _eventNamingPolicy;
    private bool _isEventNamingPolicyInitialized;
    
    /// <summary>
    /// Gets JsonNamingPolicy to use on naming police for serializing and deserializing the name of Event 
    /// </summary>
    private JsonNamingPolicy GetEventNamingPolicy()
    {
        if (_isEventNamingPolicyInitialized)
            return _eventNamingPolicy;

        var eventNamingPolicyType = EventNamingPolicy ?? NamingPolicyType.PascalCase;
        _eventNamingPolicy = NamingPolicyTypeNames.GetEventNamingPolicy(eventNamingPolicyType.ToString());
        
        _isEventNamingPolicyInitialized = true;

        return _eventNamingPolicy;
    }
    
    /// <summary>
    /// Copy the contents of a source dictionary to a target dictionary if the source is not null and target does not already contain the key.
    /// </summary>
    private void TryMergeDictionaries<TKey, TValue>(Dictionary<TKey, TValue> source, Dictionary<TKey, TValue> target)
    {
        if (source is null) return;

        foreach (var item in source)
        {
            if (!target.ContainsKey(item.Key))
                target[item.Key] = item.Value;
        }
    }

    #endregion
}