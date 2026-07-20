using FSH.Framework.Core.Exceptions;
using FSH.Modules.Sourcing.Contracts.v1.Requisitions;
using FSH.Modules.Sourcing.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Sourcing.Features.v1.Requisitions.UpdateRequisition;

public sealed class UpdateRequisitionCommandHandler(SourcingDbContext dbContext)
    : ICommandHandler<UpdateRequisitionCommand, Guid>
{
    public async ValueTask<Guid> Handle(UpdateRequisitionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var pr = await dbContext.PurchaseRequisitions
            .FirstOrDefaultAsync(p => p.Id == command.RequisitionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Requisition {command.RequisitionId} not found.");

        pr.UpdateHeader(
            command.Department,
            command.DepartmentCode,
            command.Location,
            command.LocationCode,
            command.Category,
            command.CategoryCode,
            command.Job,
            command.JobCode,
            command.Memo,
            command.CostCentre,
            command.Project,
            command.RequiredOn);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return pr.Id;
    }
}
