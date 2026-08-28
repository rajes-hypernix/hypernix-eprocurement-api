using FSH.Framework.Core.Context;
using FSH.Modules.Procurement.Contracts.Dtos;
using FSH.Modules.Procurement.Contracts.v1.Asns;
using FSH.Modules.Procurement.Data;
using FSH.Modules.Procurement.Services;
using FSH.Modules.Suppliers.Contracts.Services;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Procurement.Features.v1.Asns.ListAsns;

public sealed class ListAsnsQueryHandler(
    ProcurementDbContext dbContext,
    IVendorLookupService vendorLookup,
    ICurrentUser currentUser)
    : IQueryHandler<ListAsnsQuery, IReadOnlyList<AsnDto>>
{
    public async ValueTask<IReadOnlyList<AsnDto>> Handle(ListAsnsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var q = dbContext.Asns.AsNoTracking().Include(a => a.Lines).AsQueryable();
        if (query.PoId is Guid poId)
        {
            q = q.Where(a => a.PoId == poId);
        }

        if (currentUser.GetVendorId() is { } vendorId)
        {
            var ownedPoIds = dbContext.PurchaseOrders.AsNoTracking().Where(p => p.VendorId == vendorId).Select(p => p.Id);
            q = q.Where(a => ownedPoIds.Contains(a.PoId));
        }

        var asns = await q
            .OrderByDescending(a => a.CreatedUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var poIds = asns.Select(a => a.PoId).Distinct().ToList();
        var pos = await dbContext.PurchaseOrders
            .AsNoTracking()
            .Where(p => poIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Code, p.VendorId })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var vendors = await vendorLookup
            .GetManyAsync(pos.Select(p => p.VendorId).Distinct().ToList(), cancellationToken)
            .ConfigureAwait(false);

        var poById = pos.ToDictionary(p => p.Id);
        return [.. asns.Select(a =>
        {
            poById.TryGetValue(a.PoId, out var po);
            string? vendorName = po is not null && vendors.TryGetValue(po.VendorId, out var v) ? v.Name : null;
            return ProcurementDtoMapper.ToDto(a, po?.Code, vendorName);
        })];
    }
}
