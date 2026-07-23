using FSH.Modules.Platform.Contracts.v1.Configuration;
using FSH.Modules.Platform.Data;
using FSH.Modules.Platform.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.Incoterms.CreateIncoterm;

public sealed class CreateIncotermCommandHandler(PlatformDbContext dbContext)
    : ICommandHandler<CreateIncotermCommand, Guid>
{
    public async ValueTask<Guid> Handle(CreateIncotermCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var code = command.Code.Trim().ToUpperInvariant();
        bool exists = await dbContext.Incoterms
            .AnyAsync(i => i.Code == code, cancellationToken)
            .ConfigureAwait(false);
        if (exists)
            throw new PlatformRuleException($"Incoterm '{code}' already exists.");

        var incoterm = Incoterm.Create(command.Code, command.Name);
        dbContext.Incoterms.Add(incoterm);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return incoterm.Id;
    }
}
