namespace OrdersService.Models;

public class Order
{
    public Order()
    {
        Id = Guid.NewGuid();
    }

    public Guid Id { get; }

    public string Name { get; set; }
}