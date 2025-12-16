using EventBus.RabbitMQ.Publishers.Managers;
using EventStorage.Models;
using EventStorage.Outbox.Managers;
using Microsoft.AspNetCore.Mvc;
using UsersService.Messaging.Events.Publishers;
using UsersService.Models;

namespace UsersService.Controllers;

[ApiController]
[Route("[controller]")]
public class UserController : ControllerBase
{
    private readonly IEventPublisherManager _eventPublisherManager;
    private readonly IOutboxEventManager _outboxEventManager;

    private static readonly Dictionary<Guid, User> Items = new();

    public UserController(IEventPublisherManager eventPublisherManager,
        IOutboxEventManager outboxEventManager)
    {
        _eventPublisherManager = eventPublisherManager;
        _outboxEventManager = outboxEventManager;
    }

    [HttpGet]
    public IActionResult GetItems()
    {
        return Ok(Items.Values);
    }

    [HttpGet("{id:guid}")]
    public IActionResult GetItems(Guid id)
    {
        if (!Items.TryGetValue(id, out User item))
            return NotFound();

        return Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] User item)
    {
        Items.Add(item.Id, item);

        var userCreated = new UserCreated { UserId = item.Id, UserName = item.Name };

        //await _eventPublisherManager.PublishAsync(userCreated);
        var test = new TestEvent { EventId = Guid.NewGuid() };
        //var sent = await _outboxEventManager.StoreAsync(test);
        var successfullySent = await _outboxEventManager.StoreAsync(userCreated, EventProviderType.MessageBroker);

        return Ok();
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromQuery] string newName, CancellationToken cancellationToken)
    {
        if (!Items.TryGetValue(id, out User item))
            return NotFound();

        var userUpdated = new UserUpdated { UserId = item.Id, OldUserName = item.Name, NewUserName = newName };
        userUpdated.Headers = new();
        userUpdated.Headers.TryAdd("TraceId", HttpContext.TraceIdentifier);
        await _eventPublisherManager.PublishAsync(userUpdated, cancellationToken);

        item.Name = newName;
        return Ok(item);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (!Items.TryGetValue(id, out User item))
            return NotFound();

        var userDeleted = new UserDeleted { UserId = item.Id, UserName = item.Name };
        var successfullySent = await _outboxEventManager.StoreAsync(userDeleted, EventProviderType.MessageBroker);

        Items.Remove(id);
        return Ok(item);
    }
}