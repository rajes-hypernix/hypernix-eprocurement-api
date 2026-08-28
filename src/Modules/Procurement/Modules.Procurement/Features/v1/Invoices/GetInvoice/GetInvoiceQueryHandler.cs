using FSH.Framework.Core.Context;
using FSH.Modules.Procurement.Contracts.Dtos;
using FSH.Modules.Procurement.Contracts.v1.Invoices;
using FSH.Modules.Procurement.Data;
using FSH.Modules.Procurement.Services;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Procurement.Features.v1.Invoices.GetInvoice;

public sealed class GetInvoiceQueryHandler(ProcurementDbContext dbContext, ICurrentUser currentUser)
    : IQueryHandler<GetInvoiceQuery, InvoiceDto?>
{
    public async ValueTask<InvoiceDto?> Handle(GetInvoiceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var invoice = await dbContext.Invoices
            .AsNoTracking()
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == query.InvoiceId, cancellationToken)
            .ConfigureAwait(false);

        if (invoice is null) return null;

        var poVendorId = await dbContext.PurchaseOrders
            .AsNoTracking()
            .Where(p => p.Id == invoice.PoId)
            .Select(p => (Guid?)p.VendorId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (poVendorId is Guid vid)
        {
            currentUser.EnsureOwns(vid);
        }

        return ProcurementDtoMapper.ToDto(invoice);
    }
}
