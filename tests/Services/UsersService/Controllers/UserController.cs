using EventStorage.Models;
using EventStorage.Outbox.Managers;
using Microsoft.AspNetCore.Mvc;
using UsersService.Messaging.Events.Publishers;
using UsersService.Models;
using IEventPublisherManager = EventBus.RabbitMQ.Publishers.Managers.IEventPublisherManager;

namespace UsersService.Controllers;

[ApiController]
[Route("[controller]")]
public class UserController : ControllerBase
{
    private readonly IEventPublisherManager _eventPublisherManager;
    private readonly IEventSenderManager _eventSenderManager;

    private readonly ILogger<UserController> _logger;
    private static readonly Dictionary<Guid, User> Items = new();

    public UserController(ILogger<UserController> logger, IEventPublisherManager eventPublisherManager,
        IEventSenderManager eventSenderManager)
    {
        _logger = logger;
        _eventPublisherManager = eventPublisherManager;
        _eventSenderManager = eventSenderManager;
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
    public IActionResult Create([FromBody] User item)
    {
        Items.Add(item.Id, item);

        var userCreated = new UserCreated { UserId = item.Id, UserName = item.Name };
        //_eventPublisherManager.Publish(userCreated);
        
        var eventPath = userCreated.GetType().Name;
        var successfullySent = _eventSenderManager.Send(userCreated, EventProviderType.MessageBroker, eventPath);
        
        return Ok();
    }

    [HttpPut("{id:guid}")]
    public IActionResult Update(Guid id, [FromQuery] string newName)
    {
        if (!Items.TryGetValue(id, out User item))
            return NotFound();

        var userUpdated = new UserUpdated { UserId = item.Id, OldUserName = item.Name, NewUserName = newName };
        userUpdated.Headers = new();
        userUpdated.Headers.TryAdd("TraceId", HttpContext.TraceIdentifier);
        _eventPublisherManager.Publish(userUpdated);

        item.Name = newName;
        return Ok(item);
    }

    [HttpDelete("{id:guid}")]
    public IActionResult Delete(Guid id)
    {
        if (!Items.TryGetValue(id, out User item))
            return NotFound();

        var userDeleted = new UserDeleted { UserId = item.Id, UserName = item.Name };
        var url = "https:example.com/api/users";
        var successfullySent = _eventSenderManager.Send(userDeleted, EventProviderType.WebHook, url);
        
        var eventPath = userDeleted.GetType().Name;
        successfullySent = _eventSenderManager.Send(userDeleted, EventProviderType.MessageBroker, eventPath);
        
        Items.Remove(id);
        return Ok(item);
    }
}