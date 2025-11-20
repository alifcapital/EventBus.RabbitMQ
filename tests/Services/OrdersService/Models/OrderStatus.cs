namespace OrdersService.Models;

public enum OrderStatus
{
    None,
    ProcessingPayment,
    Delivering,
    Completed,
    Cancelled
}