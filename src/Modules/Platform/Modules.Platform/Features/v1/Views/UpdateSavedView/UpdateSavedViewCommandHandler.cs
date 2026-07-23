using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Identity.Contracts.Services;
using FSH.Modules.Platform.Contracts.Authorization;
using FSH.Modules.Platform.Contracts.Dtos;
using FSH.Modules.Platform.Contracts.v1.Views;
using FSH.Modules.Platform.Data;
using FSH.Modules.Platform.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.Views.UpdateSavedView;

public sealed class UpdateSavedViewCommandHandler(
    PlatformDbContext dbContext,
    ICurrentUser currentUser,
    IUserPermissionService permissions)
    : ICommandHandler<UpdateSavedViewCommand, SavedViewDto>
{
    public async ValueTask<SavedViewDto> Handle(UpdateSavedViewCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var view = await dbContext.SavedViews
            .Include(v => v.Filters)
            .Include(v => v.Columns)
            .FirstOrDefaultAsync(v => v.Id == command.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Saved view {command.Id} not found.");

        string userId = currentUser.GetUserId().ToString();
        bool isOwner = !view.IsSystem && view.OwnerUserId == userId;
        if (!isOwner)
        {
            bool canManageShared = await permissions
                .HasPermissionAsync(userId, PlatformPermissions.Views.ManageShared, cancellationToken)
                .ConfigureAwait(false);
            if (!canManageShared)
                throw new ForbiddenException("Only the owner (or a user who can manage shared views) can edit this saved view.");
        }

        if (!Enum.TryParse<ViewRecordType>(command.Request.RecordType, ignoreCase: true, out var recordType) || recordType != view.RecordType)
            throw new PlatformRuleException("A saved view's record type cannot be changed after creation.");

        var (_, filters, columns) = await SavedViewRequestValidation
            .ValidateAndConvertAsync(dbContext, command.Request, cancellationToken)
            .ConfigureAwait(false);

        view.UpdateDefinition(command.Request.Name, filters, columns);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return PlatformDtoMapper.ToDto(view);
    }
}
