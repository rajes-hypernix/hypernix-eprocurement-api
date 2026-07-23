using FSH.Framework.Core.Exceptions;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using FSH.Modules.Platform.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.TaxCodes.UpdateTaxCode;

public sealed class UpdateTaxCodeCommandHandler(PlatformDbContext dbContext)
    : ICommandHandler<UpdateTaxCodeCommand, Guid>
{
    public async ValueTask<Guid> Handle(UpdateTaxCodeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var taxCode = await dbContext.TaxCodes
            .FirstOrDefaultAsync(t => t.Id == command.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Tax code {command.Id} not found.");

        taxCode.Update(command.Name, command.RatePct);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return taxCode.Id;
    }
}
