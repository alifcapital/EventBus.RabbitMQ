using EventBus.RabbitMQ.Configurations;

namespace EventBus.RabbitMQ.Connections;

internal interface IRabbitMqConnectionCreator
{
    public IRabbitMqConnection CreateConnection(BaseEventOptions eventSettings, IServiceProvider serviceProvider);
}