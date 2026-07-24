using FSH.Framework.Core.Exceptions;
using FSH.Modules.Platform.Contracts.v1.EntryForms;
using FSH.Modules.Platform.Data;
using FSH.Modules.Platform.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.EntryForms.ReplaceEntryFormLayout;

public sealed class ReplaceEntryFormLayoutCommandHandler(PlatformDbContext dbContext)
    : ICommandHandler<ReplaceEntryFormLayoutCommand, Guid>
{
    public async ValueTask<Guid> Handle(ReplaceEntryFormLayoutCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var form = await dbContext.EntryFormDefs
            .Include(f => f.Groups)
            .Include(f => f.Fields)
            .FirstOrDefaultAsync(f => f.Id == command.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Entry form {command.Id} not found.");

        form.ReplaceLayout(
            [.. command.Groups.Select(g => new EntryFormGroupInput(g.Title, g.Sort))],
            [.. command.Fields.Select(f => new EntryFormFieldInput(f.FieldKey, f.GroupIndex, f.Sort, f.RequiredOnForm, f.FullWidth))]);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return form.Id;
    }
}
