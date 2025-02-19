namespace EventBus.RabbitMQ.Exceptions;

public class EventBusException : Exception
{
    public EventBusException(string message) : base(message)
    {
    }

    public EventBusException(Exception innerException, string message) : base(message, innerException)
    {
    }
}