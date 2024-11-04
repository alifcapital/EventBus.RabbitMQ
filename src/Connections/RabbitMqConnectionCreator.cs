using EventBus.RabbitMQ.Configurations;

namespace EventBus.RabbitMQ.Connections;

internal class RabbitMqConnectionCreator: IRabbitMqConnectionCreator
{
    public IRabbitMqConnection CreateConnection(BaseEventOptions eventSettings, IServiceProvider serviceProvider)
    {
        return new RabbitMqConnection(eventSettings: eventSettings, serviceProvider: serviceProvider);
    }
}