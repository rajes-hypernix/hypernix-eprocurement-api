using FSH.Modules.Platform.Contracts.v1.EntryForms;
using FSH.Modules.Platform.Data;
using FSH.Modules.Platform.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.EntryForms.CreateEntryForm;

public sealed class CreateEntryFormCommandHandler(PlatformDbContext dbContext)
    : ICommandHandler<CreateEntryFormCommand, Guid>
{
    public async ValueTask<Guid> Handle(CreateEntryFormCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        bool exists = await dbContext.EntryFormDefs.AnyAsync(f => f.Code == command.Code, cancellationToken).ConfigureAwait(false);
        if (exists)
        {
            throw new PlatformRuleException($"An entry form with code '{command.Code}' already exists.");
        }

        var recordType = CustomFieldMapping.ParseEnum<PlatformRecordType>(command.RecordType, "record type");
        if (command.IsSystem)
        {
            bool systemExists = await dbContext.EntryFormDefs
                .AnyAsync(f => f.RecordType == recordType && f.IsSystem, cancellationToken)
                .ConfigureAwait(false);
            if (systemExists)
            {
                throw new PlatformRuleException($"{recordType} already has a system default form.");
            }
        }

        var form = EntryFormDef.Create(command.Code, command.Name, recordType, command.IsSystem);
        dbContext.EntryFormDefs.Add(form);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return form.Id;
    }
}
