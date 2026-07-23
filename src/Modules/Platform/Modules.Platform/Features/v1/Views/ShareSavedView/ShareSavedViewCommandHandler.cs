using FSH.Framework.Core.Exceptions;
using FSH.Modules.Platform.Contracts.Dtos;
using FSH.Modules.Platform.Contracts.v1.Views;
using FSH.Modules.Platform.Data;
using FSH.Modules.Platform.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.Views.ShareSavedView;

public sealed class ShareSavedViewCommandHandler(PlatformDbContext dbContext)
    : ICommandHandler<ShareSavedViewCommand, SavedViewDto>
{
    public async ValueTask<SavedViewDto> Handle(ShareSavedViewCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var view = await dbContext.SavedViews
            .Include(v => v.Filters)
            .Include(v => v.Columns)
            .FirstOrDefaultAsync(v => v.Id == command.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Saved view {command.Id} not found.");

        if (view.IsSystem)
            throw new PlatformRuleException("System views are always visible to everyone; sharing has no effect on them.");

        view.SetShared(command.IsShared);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return PlatformDtoMapper.ToDto(view);
    }
}
