using FSH.Framework.Core.Exceptions;
using FSH.Modules.Procurement.Contracts.v1.PurchaseOrders;
using FSH.Modules.Procurement.Data;
using FSH.Modules.Procurement.Domain;
using FSH.Modules.Sourcing.Contracts.v1.Awards;
using FSH.Modules.Sourcing.Contracts.v1.Rfqs;
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

        var existing = await dbContext.PurchaseOrders
            .AsNoTracking()
            .Where(po => po.AwardId == award.Id)
            .Select(po => po.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (existing.Count > 0)
        {
            return existing;
        }

        var rfq = await mediator.Send(new GetRfqByIdQuery(award.RfqId), cancellationToken).ConfigureAwait(false);
        var poIds = new List<Guid>();

        foreach (var group in award.Allocations.GroupBy(a => a.VendorId))
        {
            string code = await codeGen.NextPoCodeAsync(cancellationToken).ConfigureAwait(false);
            var po = PurchaseOrder.Create(code, group.Key, rfq.Currency, PoSourceKind.FromAward, awardId: award.Id, rfqId: award.RfqId);
            po.SetDetails(rfq.IncotermId, rfq.IncotermCode, rfq.IncotermSuffix, memo: null, vendorRef: null, requiredDate: null, deliveryDate: null);

            foreach (var alloc in group)
            {
                var rfqLine = rfq.Lines.FirstOrDefault(l =>
                    string.Equals(l.LineCode, alloc.RfqLineCode, StringComparison.Ordinal)
                    || string.Equals(l.ItemCode, alloc.RfqLineCode, StringComparison.Ordinal));
                string itemCode = rfqLine?.ItemCode ?? alloc.RfqLineCode;
                string description = rfqLine?.Description ?? alloc.RfqLineCode;
                string uom = rfqLine?.Uom ?? "EA";
                po.AddLine(itemCode, description, uom, alloc.Qty, alloc.UnitPrice, alloc.RfqLineCode, priceConfirmed: true);
            }

            dbContext.PurchaseOrders.Add(po);
            poIds.Add(po.Id);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return poIds;
    }
}
