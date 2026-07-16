namespace EngineeringManager.Application.Reminders;

public interface IReminderService
{
    Task RefreshAsync(DateOnly today, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReminderDto>> ListAsync(bool includeResolved, CancellationToken cancellationToken);
    Task MarkReadAsync(Guid reminderId, CancellationToken cancellationToken);
    Task ResolveAsync(Guid reminderId, CancellationToken cancellationToken);
}
