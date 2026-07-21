using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Procurement.Contracts.Dtos;
using FSH.Modules.Procurement.Contracts.v1.Invoices;
using FSH.Modules.Procurement.Data;
using FSH.Modules.Procurement.Domain;
using FSH.Modules.Procurement.Services;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Procurement.Features.v1.Invoices.SubmitInvoice;

public sealed class SubmitInvoiceCommandHandler(
    ProcurementDbContext dbContext,
    IProcurementCodeGenerator codeGen,
    ICurrentUser currentUser)
    : ICommandHandler<SubmitInvoiceCommand, InvoiceDto>
{
    public async ValueTask<InvoiceDto> Handle(SubmitInvoiceCommand command, CancellationToken cancellationToken)
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

        var grns = await dbContext.Grns
            .Where(g => g.PoId == po.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Guid? grnId = grns.Count == 1 ? grns[0].Id : null;

        string code = await codeGen.NextInvoiceCodeAsync(cancellationToken).ConfigureAwait(false);
        var invoice = Invoice.Create(code, po.Id, grnId, command.InvoiceNo, command.Date, command.WhtRate);

        bool hasVariance = false;
        foreach (var lineInput in command.Lines)
        {
            var poLine = po.Lines.FirstOrDefault(l => string.Equals(l.ItemCode, lineInput.ItemCode, StringComparison.Ordinal));
            if (poLine is null) continue;

            decimal billable = Math.Max(0, poLine.ReceivedQty - poLine.InvoicedQty);
            decimal billableQty = Math.Max(0, Math.Min(lineInput.Qty, billable));
            if (billableQty <= 0) continue;

            if (poLine.UnitPrice > 0)
            {
                decimal variance = Math.Abs(lineInput.UnitPrice - poLine.UnitPrice) / poLine.UnitPrice;
                if (variance > Invoice.PriceTolerance)
                    hasVariance = true;
            }

            invoice.AddLine(lineInput.ItemCode, billableQty, lineInput.UnitPrice);
            poLine.AddInvoicedQty(billableQty);
        }

        if (invoice.Lines.Count == 0)
            throw new ProcurementRuleException("No billable lines on this invoice.");

        if (hasVariance)
        {
            invoice.MarkException("Unit price variance exceeds tolerance.");
            po.MarkDiscrepancy();
        }

        dbContext.Invoices.Add(invoice);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ProcurementDtoMapper.ToDto(invoice);
    }
}
