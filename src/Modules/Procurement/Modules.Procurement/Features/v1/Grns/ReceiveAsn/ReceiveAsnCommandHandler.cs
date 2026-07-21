using FSH.Framework.Core.Exceptions;
using FSH.Modules.Procurement.Contracts.Dtos;
using FSH.Modules.Procurement.Contracts.v1.Grns;
using FSH.Modules.Procurement.Data;
using FSH.Modules.Procurement.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Procurement.Features.v1.Grns.ReceiveAsn;

public sealed class ReceiveAsnCommandHandler(
    ProcurementDbContext dbContext,
    IProcurementCodeGenerator codeGen)
    : ICommandHandler<ReceiveAsnCommand, GrnDto>
{
    public async ValueTask<GrnDto> Handle(ReceiveAsnCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var asn = await dbContext.Asns
            .Include(a => a.Lines)
            .FirstOrDefaultAsync(a => a.Id == command.AsnId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"ASN {command.AsnId} not found.");

        if (asn.Status != AsnStatus.InTransit)
            throw new ProcurementRuleException($"Cannot receive an ASN that is {asn.Status}.");

        var po = await dbContext.PurchaseOrders
            .Include(p => p.Lines)
            .FirstOrDefaultAsync(p => p.Id == asn.PoId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Purchase order for ASN {command.AsnId} not found.");

        string code = await codeGen.NextGrnCodeAsync(cancellationToken).ConfigureAwait(false);
        var grn = Grn.Create(code, asn.Id, po.Id);

        foreach (var asnLine in asn.Lines)
        {
            var poLine = po.Lines.FirstOrDefault(l => string.Equals(l.ItemCode, asnLine.ItemCode, StringComparison.Ordinal));
            if (poLine is null)
            {
                continue;
            }

            decimal outstanding = Math.Max(0, poLine.Qty - poLine.ReceivedQty);
            decimal input = command.Lines
                .FirstOrDefault(x => string.Equals(x.ItemCode, asnLine.ItemCode, StringComparison.Ordinal))
                ?.ReceivedQty ?? 0m;
            decimal receivedQty = Math.Max(0, Math.Min(input, Math.Min(asnLine.ShippedQty, outstanding)));

            grn.AddLine(asnLine.ItemCode, asnLine.ShippedQty, receivedQty);
            if (receivedQty > 0)
            {
                poLine.AddReceivedQty(receivedQty);
            }
        }

        asn.MarkReceived();
        po.RecordReceipt();

        dbContext.Grns.Add(grn);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ProcurementDtoMapper.ToDto(grn);
    }
}
