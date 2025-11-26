using EventBus.RabbitMQ.Exceptions;
using RabbitMQ.Client;

namespace EventBus.RabbitMQ.Connections;

internal interface IRabbitMqConnection : IDisposable
{
    /// <summary>
    /// Returns true, when RabbitMQ connected
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// For connecting the server to the RabbitMQ.
    /// </summary>
    /// <throws cref="EventBusException">Throws <see cref="EventBusException"/> when connection cannot be opened.</throws>
    void Connect();

    /// <summary>
    /// To create a model after opening connection. If the connection is not opened yet, it will try to open.
    /// </summary>
    /// <throws cref="EventBusException">Throws <see cref="EventBusException"/> when connection cannot be opened or create model.</throws>
    /// <returns>Returns created model</returns>
    IModel CreateChannel();
}