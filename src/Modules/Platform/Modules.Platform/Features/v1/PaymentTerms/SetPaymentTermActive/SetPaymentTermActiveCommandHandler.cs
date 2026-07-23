using FSH.Framework.Core.Exceptions;
using FSH.Modules.Platform.Contracts.v1.Configuration;
using FSH.Modules.Platform.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Platform.Features.v1.PaymentTerms.SetPaymentTermActive;

public sealed class SetPaymentTermActiveCommandHandler(PlatformDbContext dbContext)
    : ICommandHandler<SetPaymentTermActiveCommand, Guid>
{
    public async ValueTask<Guid> Handle(SetPaymentTermActiveCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var term = await dbContext.PaymentTerms
            .FirstOrDefaultAsync(t => t.Id == command.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Payment term {command.Id} not found.");

        term.SetActive(command.IsActive);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return term.Id;
    }
}
