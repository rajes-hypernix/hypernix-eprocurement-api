using FSH.Modules.Platform.Contracts.v1.CustomFields;
using FSH.Modules.Platform.Data;
using FSH.Modules.Platform.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.CustomFields.CreateCustomFieldDef;

public sealed class CreateCustomFieldDefCommandHandler(PlatformDbContext dbContext)
    : ICommandHandler<CreateCustomFieldDefCommand, Guid>
{
    public async ValueTask<Guid> Handle(CreateCustomFieldDefCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        bool exists = await dbContext.CustomFieldDefs
            .AnyAsync(d => d.Code == command.Code, cancellationToken)
            .ConfigureAwait(false);
        if (exists)
        {
            throw new PlatformRuleException($"A custom field with code '{command.Code}' already exists.");
        }

        var dataType = CustomFieldMapping.ParseEnum<CustomFieldDataType>(command.DataType, "data type");
        var scope = CustomFieldMapping.ParseEnum<CustomFieldScope>(command.Scope, "scope");
        var displayType = CustomFieldMapping.ParseEnum<CustomFieldDisplayType>(command.DisplayType, "display type");
        CustomFieldRefEntity? refEntity = string.IsNullOrWhiteSpace(command.RefEntity)
            ? null
            : CustomFieldMapping.ParseEnum<CustomFieldRefEntity>(command.RefEntity, "reference entity");

        var def = CustomFieldDef.Create(
            command.Code, command.Label, dataType, scope, refEntity, command.ListKey,
            displayType, command.ShowInList, command.IsRequired, command.HelpText);

        dbContext.CustomFieldDefs.Add(def);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return def.Id;
    }
}
