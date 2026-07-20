using FSH.Framework.Core.Exceptions;
using FSH.Modules.Sourcing.Contracts.v1.Requisitions;
using FSH.Modules.Sourcing.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Sourcing.Features.v1.Requisitions.CancelRequisition;

public sealed class CancelRequisitionCommandHandler(SourcingDbContext dbContext)
    : ICommandHandler<CancelRequisitionCommand, Guid>
{
    public async ValueTask<Guid> Handle(CancelRequisitionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var pr = await dbContext.PurchaseRequisitions
            .Include(p => p.Lines)
            .FirstOrDefaultAsync(p => p.Id == command.RequisitionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Requisition {command.RequisitionId} not found.");

        pr.Cancel(DateTime.UtcNow);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return pr.Id;
    }
}
