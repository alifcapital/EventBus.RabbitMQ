using RabbitMQ.Client;

namespace EventBus.RabbitMQ.Connections;

internal interface IRabbitMqConnection : IDisposable
{
    /// <summary>
    /// Returns true, when RabbitMQ connected
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// For connecting the server to the RabbitMQ
    /// </summary>
    /// <returns>Returns true, if it's successful connected</returns>
    bool TryConnect();

    /// <summary>
    /// To create a model after opening connection. If the connection is not opened yet, it will try to open.
    /// </summary>
    /// <returns>Returns created model</returns>
    IModel CreateChannel();
}