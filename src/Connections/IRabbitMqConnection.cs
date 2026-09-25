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
    /// To create a channel for receiving events after opening connection. If the connection is not opened yet, it will try to open.
    /// The publisher confirmation is always disabled, since the channel is used only for receiving events.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <throws cref="EventBusException">Throws <see cref="EventBusException"/> when connection cannot be opened or create channel.</throws>
    /// <returns>Returns created channel</returns>
    Task<IChannel> CreateConsumerChannelAsync(CancellationToken cancellationToken);

    /// <summary>
    /// To create a channel for publishing events after opening connection. If the connection is not opened yet, it will try to open.
    /// </summary>
    /// <param name="publisherConfirmation">
    /// Whether the created channel should wait for the acknowledgment of the RabbitMQ broker for each published event.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <throws cref="EventBusException">Throws <see cref="EventBusException"/> when connection cannot be opened or create channel.</throws>
    /// <returns>Returns created channel</returns>
    Task<IChannel> CreatePublisherChannelAsync(bool publisherConfirmation, CancellationToken cancellationToken);
}
