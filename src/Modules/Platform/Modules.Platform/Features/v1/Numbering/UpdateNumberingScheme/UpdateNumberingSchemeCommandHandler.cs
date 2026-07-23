using FSH.Framework.Core.Exceptions;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using FSH.Modules.Platform.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.Numbering.UpdateNumberingScheme;

public sealed class UpdateNumberingSchemeCommandHandler(PlatformDbContext dbContext)
    : ICommandHandler<UpdateNumberingSchemeCommand, Guid>
{
    public async ValueTask<Guid> Handle(UpdateNumberingSchemeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var recordType = command.RecordType.Trim().ToUpperInvariant();
        var scheme = await dbContext.NumberingSchemes
            .FirstOrDefaultAsync(s => s.RecordType == recordType, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Numbering scheme '{recordType}' not found.");

        scheme.Update(command.Prefix, command.YearSegment, command.Digits);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return scheme.Id;
    }
}
