using FSH.Framework.Web.Realtime;
using FSH.Modules.Notifications.Contracts.Services;
using FSH.Modules.Notifications.Data;
using FSH.Modules.Notifications.Domain;
using Microsoft.AspNetCore.SignalR;

namespace FSH.Modules.Notifications.Services;

public sealed class InboxNotifier(
    NotificationsDbContext db,
    IHubContext<AppHub> hub) : IInboxNotifier
{
    public async Task<Guid> NotifyAsync(
        string userId,
        string type,
        string title,
        string? body,
        string? link,
        string source,
        object? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var notification = Notification.Create(userId, type, title, body, link, source, metadata);
        db.Notifications.Add(notification);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await hub.Clients.Group($"user:{userId}")
            .SendAsync("NotificationCreated", new
            {
                id = notification.Id,
                type = notification.Type,
                title = notification.Title,
                body = notification.Body,
                link = notification.Link,
                source = notification.Source,
                createdAtUtc = notification.CreatedAtUtc,
            }, cancellationToken)
            .ConfigureAwait(false);

        return notification.Id;
    }
}
