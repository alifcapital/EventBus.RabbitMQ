namespace OrdersService.Models;

public class Order
{
    public Order()
    {
        Id = Guid.NewGuid();
    }

    public Guid Id { get; }

    public string ProductName { get; set; }

    public int Quantity { get; set; }

    public decimal Price { get; set; }

    public decimal TotalPrice => Quantity * Price;

    public string CustomerEmail { get; init; } = "alif@alif.tj";
    
    public OrderStatus Status { get; set; } = OrderStatus.None;
    
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}