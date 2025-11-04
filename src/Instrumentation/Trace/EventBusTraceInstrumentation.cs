using System.Diagnostics;

namespace EventBus.RabbitMQ.Instrumentation.Trace;

/// <summary>
/// The EventBus instrumentation to create a trace activity 
/// </summary>
internal class EventBusTraceInstrumentation
{
    /// <summary>
    /// The instrumentation name
    /// </summary>
    internal const string InstrumentationName = "EventBus.RabbitMQ";
    
    /// <summary>
    /// The key to add/read the id of activity (parent trace and span) to/from the publishing/received events.
    /// </summary>
    public const string TraceParentIdKey = "TraceParentId";
    
    /// <summary>
    /// Option to enable or disable adding a payload of event to a trace.
    /// </summary>
    public static bool ShouldAttachEventPayload { get; set; }
    
    /// <summary>
    /// Option to enable or disable adding a headers of event to a trace.
    /// </summary>
    public static bool ShouldAttachEventHeaders { get; set; }

    /// <summary>
    /// The activity source to create a new activity
    /// </summary>
    private static readonly ActivitySource ActivitySource = new(InstrumentationName);

    /// <summary>
    /// For creating activity and use it to add a span.
    /// Also adds span type for being able to filter the spans from the tracing system.
    /// </summary>
    /// <param name="name">Name of new activity</param>
    /// <param name="kind">Type of new activity. The default is <see cref="ActivityKind.Internal"/></param>
    /// <param name="traceParentId">The id of activity (parent trace and span) to assign. Example: "{version}-{trace-id}-{parent-span-id}-{trace-flags}"</param>
    /// <returns>Newly created an open telemetry activity</returns>
    internal static Activity StartActivity(string name, ActivityKind kind = ActivityKind.Internal, string traceParentId = null)
    {
        ActivityContext.TryParse(traceParentId, null, out var parentContext);
        var activity = ActivitySource.StartActivity(name, kind, parentContext);
        if (activity == null) return null;
        
        const string spanType = "RabbitMQ";
        activity.AddTag(EventBusInvestigationTagNames.TraceMessagingTagName, spanType);

        return activity;
    }
}