using FSH.Framework.Core.Context;
using FSH.Modules.Platform.Contracts.Dtos;
using FSH.Modules.Platform.Contracts.v1.Views;
using FSH.Modules.Platform.Data;
using FSH.Modules.Platform.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.Views.CreateSavedView;

public sealed class CreateSavedViewCommandHandler(PlatformDbContext dbContext, ICurrentUser currentUser)
    : ICommandHandler<CreateSavedViewCommand, SavedViewDto>
{
    public async ValueTask<SavedViewDto> Handle(CreateSavedViewCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var (recordType, filters, columns) = await SavedViewRequestValidation
            .ValidateAndConvertAsync(dbContext, command.Request, cancellationToken)
            .ConfigureAwait(false);

        string code = await NextViewCodeAsync(cancellationToken).ConfigureAwait(false);
        string ownerUserId = currentUser.GetUserId().ToString();

        var view = SavedView.Create(code, command.Request.Name, recordType, ownerUserId, isShared: false, isSystem: false, filters, columns);
        dbContext.SavedViews.Add(view);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return PlatformDtoMapper.ToDto(view);
    }

    private async Task<string> NextViewCodeAsync(CancellationToken cancellationToken)
    {
        string qualifiedName = $"{PlatformDbContext.Schema}.\"ViewCodeSeq\"";
        long next = await dbContext.Database
            .SqlQuery<long>($"SELECT nextval({qualifiedName}) AS \"Value\"")
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);
        return $"VIEW-{DateTime.UtcNow.Year}-{next:D4}";
    }
}
