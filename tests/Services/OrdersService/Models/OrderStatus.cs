namespace OrdersService.Models;

public enum OrderStatus
{
    Submitted,
    ProcessingPayment,
    Delivering,
    Completed,
    Cancelled
}