using EventStorage.Management;
using Microsoft.AspNetCore.Mvc;

namespace UsersService.Controllers;

[Route("api/inbox-events")]
public class InboxEventsController(IInboxEventsService inboxEventsService) : BaseEventsController(inboxEventsService);
