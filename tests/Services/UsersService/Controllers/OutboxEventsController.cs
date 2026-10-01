using EventStorage.Management;
using Microsoft.AspNetCore.Mvc;

namespace UsersService.Controllers;

[Route("api/outbox-events")]
public class OutboxEventsController(IOutboxEventsService outboxEventsService) : BaseEventsController(outboxEventsService);
