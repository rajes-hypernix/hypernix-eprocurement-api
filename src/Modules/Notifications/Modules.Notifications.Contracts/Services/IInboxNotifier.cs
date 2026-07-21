namespace FSH.Modules.Notifications.Contracts.Services;

/// <summary>
/// Writes a bell-inbox row and pushes <c>NotificationCreated</c> over SignalR.
/// Used by Chat mentions and Modules.Communication eProcure handlers.
/// </summary>
public interface IInboxNotifier
{
    Task<Guid> NotifyAsync(
        string userId,
        string type,
        string title,
        string? body,
        string? link,
        string source,
        object? metadata = null,
        CancellationToken cancellationToken = default);
}
