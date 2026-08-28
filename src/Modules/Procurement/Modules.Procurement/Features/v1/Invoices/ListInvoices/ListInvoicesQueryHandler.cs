using FSH.Framework.Core.Context;
using FSH.Modules.Procurement.Contracts.Dtos;
using FSH.Modules.Procurement.Contracts.v1.Invoices;
using FSH.Modules.Procurement.Data;
using FSH.Modules.Procurement.Services;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Procurement.Features.v1.Invoices.ListInvoices;

public sealed class ListInvoicesQueryHandler(ProcurementDbContext dbContext, ICurrentUser currentUser)
    : IQueryHandler<ListInvoicesQuery, IReadOnlyList<InvoiceDto>>
{
    public async ValueTask<IReadOnlyList<InvoiceDto>> Handle(ListInvoicesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var q = dbContext.Invoices.AsNoTracking().Include(i => i.Lines).AsQueryable();
        if (currentUser.GetVendorId() is { } vendorId)
        {
            var poIds = dbContext.PurchaseOrders.AsNoTracking().Where(p => p.VendorId == vendorId).Select(p => p.Id);
            q = q.Where(i => poIds.Contains(i.PoId));
        }

        var invoices = await q
            .OrderByDescending(i => i.CreatedUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. invoices.Select(ProcurementDtoMapper.ToDto)];
    }
}
