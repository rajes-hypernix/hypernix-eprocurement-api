using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Procurement.Contracts.Dtos;
using FSH.Modules.Procurement.Contracts.v1.Asns;
using FSH.Modules.Procurement.Data;
using FSH.Modules.Procurement.Domain;
using FSH.Modules.Procurement.Services;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Procurement.Features.v1.Asns.CreateAsn;

public sealed class CreateAsnCommandHandler(
    ProcurementDbContext dbContext,
    IProcurementCodeGenerator codeGen,
    ICurrentUser currentUser)
    : ICommandHandler<CreateAsnCommand, AsnDto>
{
    public async ValueTask<AsnDto> Handle(CreateAsnCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var vendorId = currentUser.RequireVendorId();

        var po = await dbContext.PurchaseOrders
            .Include(p => p.Lines)
            .FirstOrDefaultAsync(p => p.Id == command.PoId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Purchase order {command.PoId} not found.");

        if (po.VendorId != vendorId)
            throw new NotFoundException($"Purchase order {command.PoId} not found.");

        if (po.Status != PoStatus.Acknowledged && po.Status != PoStatus.PartiallyReceived)
            throw new ProcurementRuleException($"Cannot create ASN for a PO that is {po.Status}.");

        var inTransitByItem = await dbContext.Asns
            .AsNoTracking()
            .Where(a => a.PoId == po.Id && a.Status == AsnStatus.InTransit)
            .SelectMany(a => a.Lines)
            .GroupBy(l => l.ItemCode)
            .Select(g => new { ItemCode = g.Key, Qty = g.Sum(x => x.ShippedQty) })
            .ToDictionaryAsync(x => x.ItemCode, x => x.Qty, cancellationToken)
            .ConfigureAwait(false);

        string code = await codeGen.NextAsnCodeAsync(cancellationToken).ConfigureAwait(false);
        var asn = Asn.Create(code, po.Id, command.Carrier, command.TrackingNo, command.ShippedDate, command.ExpectedDate);

        foreach (var lineInput in command.Lines)
        {
            var poLine = po.Lines.FirstOrDefault(l => string.Equals(l.ItemCode, lineInput.ItemCode, StringComparison.Ordinal));
            if (poLine is null)
            {
                continue;
            }

            decimal inTransit = inTransitByItem.GetValueOrDefault(lineInput.ItemCode);
            decimal remaining = Math.Max(0, poLine.Qty - inTransit - poLine.ReceivedQty);
            decimal shippedQty = Math.Max(0, Math.Min(lineInput.ShippedQty, remaining));
            if (shippedQty > 0)
            {
                asn.AddLine(lineInput.ItemCode, shippedQty, lineInput.LotNo);
            }
        }

        if (asn.Lines.Count == 0)
            throw new ProcurementRuleException("Nothing left to ship — all lines are already in transit or received.");

        dbContext.Asns.Add(asn);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ProcurementDtoMapper.ToDto(asn, po.Code);
    }
}
