using FSH.Framework.Core.Exceptions;
using FSH.Modules.Procurement.Contracts.v1.PurchaseOrders;
using FSH.Modules.Procurement.Data;
using FSH.Modules.Procurement.Domain;
using FSH.Modules.Sourcing.Contracts.v1.Awards;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Procurement.Features.v1.PurchaseOrders.CreateFromAward;

public sealed class CreatePurchaseOrdersFromAwardCommandHandler(
    ProcurementDbContext dbContext,
    IProcurementCodeGenerator codeGen,
    IMediator mediator)
    : ICommandHandler<CreatePurchaseOrdersFromAwardCommand, IReadOnlyList<Guid>>
{
    public async ValueTask<IReadOnlyList<Guid>> Handle(CreatePurchaseOrdersFromAwardCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var award = await mediator.Send(new GetAwardQuery(command.RfqId), cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"No award found for RFQ {command.RfqId}.");

        if (!string.Equals(award.Status, "Approved", StringComparison.Ordinal))
            throw new ProcurementRuleException("Award must be Approved before creating purchase orders.");

        bool alreadyExists = await dbContext.PurchaseOrders
            .AnyAsync(po => po.AwardId == award.Id, cancellationToken)
            .ConfigureAwait(false);
        if (alreadyExists)
            throw new ProcurementRuleException($"Purchase orders already exist for award {award.Code}.");

        var vendorGroups = award.Allocations.GroupBy(a => a.VendorId);
        var poIds = new List<Guid>();

        foreach (var group in vendorGroups)
        {
            string code = await codeGen.NextPoCodeAsync(cancellationToken).ConfigureAwait(false);
            var po = PurchaseOrder.Create(code, award.Id, award.RfqId, group.Key, "MYR");

            foreach (var alloc in group)
            {
                po.AddLine(alloc.RfqLineCode, alloc.RfqLineCode, "EA", alloc.Qty, alloc.UnitPrice, alloc.RfqLineCode);
            }

            dbContext.PurchaseOrders.Add(po);
            poIds.Add(po.Id);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return poIds;
    }
}
