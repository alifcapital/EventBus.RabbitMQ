using InMemoryMessaging.Managers;
using Microsoft.AspNetCore.Mvc;
using OrdersService.Infrastructure;
using OrdersService.Messaging.Events.Publishers;
using OrdersService.Models;

namespace OrdersService.Controllers;

[ApiController]
[Route("[controller]")]
public class OrderController(
    IMessageManager messageManager,
    OrderContext orderContext)
    : ControllerBase
{
    [HttpGet]
    public IActionResult GetItems()
    {
        var orders = orderContext.Orders.ToArray();
        return Ok(orders);
    }

    [HttpGet("{id:guid}")]
    public IActionResult GetById(Guid id)
    {
        var order = orderContext.Orders.FirstOrDefault(u => u.Id == id);
        if (order is null)
            return NotFound();

        return Ok(order);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Order item)
    {
        orderContext.Orders.Add(item);

        var orderSubmitted = new OrderSubmitted
        {
            OrderId = item.Id,
            TotalPrice = item.TotalPrice,
            CustomerEmail = item.CustomerEmail
        };
        await messageManager.PublishAsync(orderSubmitted);

        await orderContext.SaveChangesAsync();

        return Ok();
    }
}