namespace UsersService.Models;

/// <summary>
/// The body of the inbox/outbox event actions.
/// </summary>
public record EventActionModel
{
    /// <summary>
    /// The name of the user who performs the action.
    /// </summary>
    public string PerformedBy { get; init; }

    /// <summary>
    /// Why the action is performed.
    /// </summary>
    public string Comment { get; init; }
}
