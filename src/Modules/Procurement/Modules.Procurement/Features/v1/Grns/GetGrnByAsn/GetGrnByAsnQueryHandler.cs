using FSH.Framework.Core.Context;
using FSH.Modules.Procurement.Contracts.Dtos;
using FSH.Modules.Procurement.Contracts.v1.Grns;
using FSH.Modules.Procurement.Data;
using FSH.Modules.Procurement.Services;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Procurement.Features.v1.Grns.GetGrnByAsn;

public sealed class GetGrnByAsnQueryHandler(ProcurementDbContext dbContext, ICurrentUser currentUser)
    : IQueryHandler<GetGrnByAsnQuery, GrnDto?>
{
    public async ValueTask<GrnDto?> Handle(GetGrnByAsnQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var grn = await dbContext.Grns
            .AsNoTracking()
            .Include(g => g.Lines)
            .FirstOrDefaultAsync(g => g.AsnId == query.AsnId, cancellationToken)
            .ConfigureAwait(false);

        if (grn is null) return null;

        var poVendorId = await (
                from a in dbContext.Asns.AsNoTracking()
                join p in dbContext.PurchaseOrders.AsNoTracking() on a.PoId equals p.Id
                where a.Id == query.AsnId
                select (Guid?)p.VendorId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (poVendorId is Guid vid)
        {
            currentUser.EnsureOwns(vid);
        }

        return ProcurementDtoMapper.ToDto(grn);
    }
}
