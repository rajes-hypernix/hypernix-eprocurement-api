using FSH.Framework.Core.Exceptions;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using FSH.Modules.Platform.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.TaxCodes.SetTaxCodeActive;

public sealed class SetTaxCodeActiveCommandHandler(PlatformDbContext dbContext)
    : ICommandHandler<SetTaxCodeActiveCommand, Guid>
{
    public async ValueTask<Guid> Handle(SetTaxCodeActiveCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var taxCode = await dbContext.TaxCodes
            .FirstOrDefaultAsync(t => t.Id == command.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Tax code {command.Id} not found.");

        taxCode.SetActive(command.IsActive);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return taxCode.Id;
    }
}
