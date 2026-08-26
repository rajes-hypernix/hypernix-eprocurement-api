using FSH.Modules.Procurement.Contracts.Dtos;
using FSH.Modules.Procurement.Contracts.v1.Asns;
using FSH.Modules.Procurement.Data;
using FSH.Modules.Suppliers.Contracts.Services;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Procurement.Features.v1.Asns.GetAsn;

public sealed class GetAsnQueryHandler(ProcurementDbContext dbContext, IVendorLookupService vendorLookup)
    : IQueryHandler<GetAsnQuery, AsnDto?>
{
    public async ValueTask<AsnDto?> Handle(GetAsnQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var asn = await dbContext.Asns
            .AsNoTracking()
            .Include(a => a.Lines)
            .FirstOrDefaultAsync(a => a.Id == query.AsnId, cancellationToken)
            .ConfigureAwait(false);

        if (asn is null) return null;

        var po = await dbContext.PurchaseOrders
            .AsNoTracking()
            .Include(p => p.Lines)
            .FirstOrDefaultAsync(p => p.Id == asn.PoId, cancellationToken)
            .ConfigureAwait(false);

        string? vendorName = null;
        if (po is not null)
        {
            var vendors = await vendorLookup.GetManyAsync([po.VendorId], cancellationToken).ConfigureAwait(false);
            vendorName = vendors.TryGetValue(po.VendorId, out var v) ? v.Name : null;
        }

        return ProcurementDtoMapper.ToDto(asn, po?.Code, vendorName, po);
    }
}
