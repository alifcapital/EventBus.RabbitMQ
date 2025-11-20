using EventBus.RabbitMQ.Publishers.Managers;
using EventStorage.Models;
using Microsoft.AspNetCore.Mvc;
using OrdersService.Infrastructure;
using OrdersService.Models;

namespace OrdersService.Controllers;

[ApiController]
[Route("[controller]")]
public class OrderController(
    IEventPublisherManager eventPublisherManager,
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

        // var userCreated = new UserCreated { UserId = item.Id, UserName = item.Name };
        //
        // var successfullySent = await outboxEventManager.StoreAsync(userCreated, EventProviderType.MessageBroker);

        await orderContext.SaveChangesAsync();

        return Ok();
    }
}