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
    Task ConnectAsync(CancellationToken cancellationToken);

    /// <summary>
    /// To create a model after opening connection. If the connection is not opened yet, it will try to open.
    /// </summary>
    /// <param name="publisherConfirmation">
    /// Whether the created channel should wait for the acknowledgment of the RabbitMQ broker for each published event.
    /// It makes sense only for the channels which are used for publishing an event.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <throws cref="EventBusException">Throws <see cref="EventBusException"/> when connection cannot be opened or create model.</throws>
    /// <returns>Returns created model</returns>
    Task<IChannel> CreateChannelAsync(bool publisherConfirmation, CancellationToken cancellationToken);
}
