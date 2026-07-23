using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Identity.Contracts.Services;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.v1.Views;
using FSH.Modules.Platform.Data;
using FSH.Modules.Platform.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.Views.DeleteSavedView;

public sealed class DeleteSavedViewCommandHandler(
    PlatformDbContext dbContext,
    ICurrentUser currentUser,
    IUserPermissionService permissions)
    : ICommandHandler<DeleteSavedViewCommand, Unit>
{
    public async ValueTask<Unit> Handle(DeleteSavedViewCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var view = await dbContext.SavedViews
            .FirstOrDefaultAsync(v => v.Id == command.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Saved view {command.Id} not found.");

        if (view.IsSystem)
            throw new PlatformRuleException("System views cannot be deleted.");

        string userId = currentUser.GetUserId().ToString();
        if (view.OwnerUserId != userId)
        {
            bool canManageShared = await permissions
                .HasPermissionAsync(userId, PlatformPermissions.Views.ManageShared, cancellationToken)
                .ConfigureAwait(false);
            if (!canManageShared)
                throw new ForbiddenException("Only the owner (or a user who can manage shared views) can delete this saved view.");
        }

        dbContext.SavedViews.Remove(view);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}
