using EventBus.RabbitMQ.Instrumentation.Trace;
using OpenTelemetry.Trace;

namespace EventBus.RabbitMQ.Instrumentation;

public static class InstrumentationBuilderExtensions
{
    /// <summary>
    /// Enables the incoming and outgoing events automatic data collection for ASP.NET Core.
    /// </summary>
    /// <param name="builder"><see cref="T:OpenTelemetry.Trace.TracerProviderBuilder" /> being configured.</param>
    /// <param name="shouldAttachEventPayload">Option to enable or disable adding a payload of event to a trace. Default is true.</param>
    /// <param name="shouldAttachEventHeaders">Option to enable or disable adding a headers of event to a trace. Default is true.</param>
    /// <returns>The instance of <see cref="T:OpenTelemetry.Trace.TracerProviderBuilder" /> to chain the calls.</returns>
    public static TracerProviderBuilder AddEventBusInstrumentation(this TracerProviderBuilder builder, bool shouldAttachEventPayload = true, bool shouldAttachEventHeaders = true)
    {
        builder.AddSource(EventBusTraceInstrumentation.InstrumentationName);
        EventBusTraceInstrumentation.ShouldAttachEventPayload = shouldAttachEventPayload;
        EventBusTraceInstrumentation.ShouldAttachEventHeaders = shouldAttachEventHeaders;

        return builder;
    }
}