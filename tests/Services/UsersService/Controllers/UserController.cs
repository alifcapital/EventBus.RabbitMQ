using EventBus.RabbitMQ.Publishers.Managers;
using EventStorage.Models;
using EventStorage.Outbox.Managers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UsersService.Infrastructure;
using UsersService.Messaging.Events.Publishers;
using UsersService.Models;

namespace UsersService.Controllers;

[ApiController]
[Route("[controller]")]
public class UserController(
    IEventPublisherManager eventPublisherManager,
    IOutboxEventManager outboxEventManager,
    UserContext userContext)
    : ControllerBase
{
    [HttpGet]
    public IActionResult GetItems()
    {
        var users = userContext.Users.ToArray();
        return Ok(users);
    }

    [HttpGet("{id:guid}")]
    public IActionResult GetById(Guid id)
    {
        var user = userContext.Users.FirstOrDefault(u => u.Id == id);
        if (user is null)
            return NotFound();

        return Ok(user);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] User item)
    {
        userContext.Users.Add(item);

        var userCreated = new UserCreated { UserId = item.Id, UserName = item.Name };

        //await _eventPublisherManager.PublishAsync(userCreated);
        var test = new TestEvent { EventId = Guid.NewGuid() };
        //var sent = await _outboxEventManager.StoreAsync(test);
        var successfullySent = await outboxEventManager.StoreAsync(userCreated, EventProviderType.MessageBroker);
 
        await userContext.SaveChangesAsync();

        return Ok();
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromQuery] string newName)
    {
        var user = userContext.Users.AsTracking().FirstOrDefault(u => u.Id == id);
        if (user is null)
            return NotFound();

        var userUpdated = new UserUpdated { UserId = user.Id, OldUserName = user.Name, NewUserName = newName };
        userUpdated.Headers = new();
        userUpdated.Headers.TryAdd("TraceId", HttpContext.TraceIdentifier);
        await eventPublisherManager.PublishAsync(userUpdated);

        user.Name = newName;
        await userContext.SaveChangesAsync();
        
        return Ok(user);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var user = userContext.Users.FirstOrDefault(u => u.Id == id);
        if (user is null)
            return NotFound();

        var userDeleted = new UserDeleted { UserId = user.Id, UserName = user.Name };
        var successfullySent = await outboxEventManager.StoreAsync(userDeleted, EventProviderType.MessageBroker);

        userContext.Users.Remove(user);
        await userContext.SaveChangesAsync();
        
        return Ok(user);
    }
}