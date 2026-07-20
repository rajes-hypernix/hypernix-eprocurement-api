using FSH.Modules.Sourcing.Contracts.Dtos;
using FSH.Modules.Sourcing.Contracts.v1.Requisitions;
using FSH.Modules.Sourcing.Data;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Sourcing.Features.v1.Requisitions.ListRequisitions;

public sealed class ListRequisitionsQueryHandler(SourcingDbContext dbContext)
    : IQueryHandler<ListRequisitionsQuery, IReadOnlyList<RequisitionListItemDto>>
{
    public async ValueTask<IReadOnlyList<RequisitionListItemDto>> Handle(ListRequisitionsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var requisitions = await dbContext.PurchaseRequisitions
            .AsNoTracking()
            .Include(p => p.Lines)
            .OrderByDescending(p => p.CreatedUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. requisitions.Select(RequisitionDtoMapper.ToListItemDto)];
    }
}
