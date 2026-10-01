using EventStorage.Management;
using EventStorage.Management.Models;
using Microsoft.AspNetCore.Mvc;
using UsersService.Models;

namespace UsersService.Controllers;

/// <summary>
/// The base controller to view and manage inbox/outbox events by the <see cref="IEventsManagementService"/>.
/// It is a test service, so there is no authorization. In a real application, protect every endpoint with its own permission.
/// </summary>
[ApiController]
public abstract class BaseEventsController(IEventsManagementService eventsService) : ControllerBase
{
    [HttpGet]
    public Task<EventPagedList<EventDetails>> GetEvents([FromQuery] EventsFilter filter,
        CancellationToken cancellationToken)
        => eventsService.GetEventsAsync(filter, cancellationToken);

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetEvent(Guid id, CancellationToken cancellationToken)
    {
        var eventDetails = await eventsService.GetEventByIdAsync(id, cancellationToken);
        return eventDetails is null ? NotFound() : Ok(eventDetails);
    }

    [HttpGet("provider-types")]
    public string[] GetProviderTypes() => eventsService.GetProviderTypes();

    [HttpPost("{id:guid}/execute")]
    public async Task<IActionResult> Execute(Guid id, [FromBody] EventActionRequest request,
        CancellationToken cancellationToken)
        => ToResponse(await eventsService.ExecuteAsync(id, request, cancellationToken));

    /// <summary>
    /// Runs the event again even if it is already processed. It may repeat the side effects of the event.
    /// </summary>
    [HttpPost("{id:guid}/force-execute")]
    public async Task<IActionResult> ForceExecute(Guid id, [FromBody] EventActionRequest request,
        CancellationToken cancellationToken)
        => ToResponse(await eventsService.ExecuteAsync(id, request,
            cancellationToken));

    [HttpPost("{id:guid}/reschedule")]
    public async Task<IActionResult> Reschedule(Guid id, [FromQuery] DateTime tryAfterAt,
        [FromBody] EventActionRequest request, CancellationToken cancellationToken)
        => ToResponse(await eventsService.RescheduleAsync(id, tryAfterAt, request, cancellationToken));

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] EventActionRequest request,
        CancellationToken cancellationToken)
        => ToResponse(await eventsService.RejectAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/mark-as-processed")]
    public async Task<IActionResult> MarkAsProcessed(Guid id, [FromBody] EventActionRequest request,
        CancellationToken cancellationToken)
        => ToResponse(await eventsService.MarkAsProcessedAsync(id, request, cancellationToken));

    #region Helper methods


    private IActionResult ToResponse(EventActionResult result) => result.Status switch
    {
        EventActionResultStatus.Success => Ok(),
        EventActionResultStatus.NotFound => NotFound(result.FailureReason),
        EventActionResultStatus.AlreadyProcessing or EventActionResultStatus.InvalidState =>
            Conflict(result.FailureReason),
        _ => UnprocessableEntity(result.FailureReason)
    };

    #endregion
}
