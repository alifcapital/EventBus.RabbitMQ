using EventStorage.Instrumentation;

namespace EventBus.RabbitMQ.Instrumentation;

/// <summary>
/// This struct defines a set of investigation tag names as constants to avoid hardcoding strings while attaching tags to the open telemetry.
/// It can be extended from the other modules to add more tag names as needed.
/// </summary>
public class EventBusInvestigationTagNames : EventStorageInvestigationTagNames
{
    /// <summary>
    /// The tag to add the payload of event to a trace. Since it will use to add an open telemetry event, it should be uppercase.
    /// </summary>
    internal const string EventPayloadTag = "Event.Payload";
    
    /// <summary>
    /// The tag to add the headers of event to a trace. Since it will use to add an open telemetry event, it should be uppercase.
    /// </summary>
    internal const string EventHeadersTag = "Event.Headers";
    
    /// <summary>
    /// The tag to add the virtual host name of RabbitMQ to a trace.
    /// </summary>
    internal const string EventHostNameTag = "event.virtual-host-name";
    
    /// <summary>
    /// The tag to add the exchange name of RabbitMQ to a trace.
    /// </summary>
    internal const string EventExchangeNameTag = "event.exchange-name";
    
    /// <summary>
    /// The tag to add the routing key of RabbitMQ to a trace.
    /// </summary>
    internal const string EventRoutingKeyTag = "event.routing-key";
    
    /// <summary>
    /// The tag to add the received type name of event to a trace.
    /// </summary>
    internal const string ReceivedEventTypeTag = "event.received-type";
    
    /// <summary>
    /// The tag to add the received routing key of RabbitMQ to a trace.
    /// </summary>
    internal const string ReceivedEventRoutingKeyTag = "event.received-routing-key";
    
    /// <summary>
    /// The tag to add the received event id of RabbitMQ to a trace.
    /// </summary>
    internal const string ReceivedEventIdTag = "event.received-id";
}