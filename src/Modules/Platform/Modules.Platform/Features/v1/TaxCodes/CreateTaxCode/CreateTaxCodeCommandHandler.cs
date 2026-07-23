using FSH.Modules.Platform.Contracts.v1.Configuration;
using FSH.Modules.Platform.Data;
using FSH.Modules.Platform.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.TaxCodes.CreateTaxCode;

public sealed class CreateTaxCodeCommandHandler(PlatformDbContext dbContext)
    : ICommandHandler<CreateTaxCodeCommand, Guid>
{
    public async ValueTask<Guid> Handle(CreateTaxCodeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var code = command.Code.Trim().ToUpperInvariant();
        bool exists = await dbContext.TaxCodes
            .AnyAsync(t => t.Code == code, cancellationToken)
            .ConfigureAwait(false);
        if (exists)
            throw new PlatformRuleException($"Tax code '{code}' already exists.");

        var taxCode = TaxCode.Create(command.Code, command.Name, command.RatePct);
        dbContext.TaxCodes.Add(taxCode);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return taxCode.Id;
    }
}
