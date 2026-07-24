using FSH.Framework.Core.Exceptions;
using FSH.Modules.Platform.Contracts.v1.CustomFields;
using FSH.Modules.Platform.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.CustomFields.UpdateCustomFieldDef;

public sealed class UpdateCustomFieldDefCommandHandler(PlatformDbContext dbContext)
    : ICommandHandler<UpdateCustomFieldDefCommand, Guid>
{
    public async ValueTask<Guid> Handle(UpdateCustomFieldDefCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var def = await dbContext.CustomFieldDefs
            .FirstOrDefaultAsync(d => d.Id == command.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Custom field {command.Id} not found.");

        var displayType = CustomFieldMapping.ParseEnum<Domain.CustomFieldDisplayType>(command.DisplayType, "display type");
        def.UpdateDetails(command.Label, displayType, command.ShowInList, command.IsRequired, command.HelpText);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return def.Id;
    }
}
