using FSH.Framework.Shared.Persistence;
using FSH.Modules.Suppliers.Contracts.Dtos;
using FSH.Modules.Suppliers.Contracts.v1.Vendors;
using FSH.Modules.Suppliers.Data;
using FSH.Modules.Suppliers.Domain;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Suppliers.Features.v1.Vendors.SearchVendors;

public sealed class SearchVendorsQueryHandler(SuppliersDbContext dbContext)
    : IQueryHandler<SearchVendorsQuery, PagedResponse<VendorListItemDto>>
{
    public async ValueTask<PagedResponse<VendorListItemDto>> Handle(SearchVendorsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var q = dbContext.Vendors.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            string term = query.Search.Trim();
            q = q.Where(v =>
                EF.Functions.ILike(v.Code, $"%{term}%") ||
                EF.Functions.ILike(v.Name, $"%{term}%") ||
                EF.Functions.ILike(v.RegisteredName, $"%{term}%"));
        }

        q = ApplySort(q, query.SortBy, query.SortDir);

        long total = await q.LongCountAsync(cancellationToken).ConfigureAwait(false);
        int skip = (query.PageNumber - 1) * query.PageSize;
        var vendors = await q.Skip(skip).Take(query.PageSize).ToListAsync(cancellationToken).ConfigureAwait(false);

        return new PagedResponse<VendorListItemDto>
        {
            Items = vendors
                .Select(v => new VendorListItemDto(
                    v.Id,
                    v.Code,
                    v.Name,
                    v.Type,
                    v.Categories,
                    v.Region,
                    v.State,
                    v.Rating,
                    v.Status))
                .ToList(),
            PageNumber = query.PageNumber,
            PageSize = query.PageSize,
            TotalCount = total,
            TotalPages = (int)Math.Ceiling(total / (double)query.PageSize)
        };
    }

    private static IQueryable<Vendor> ApplySort(IQueryable<Vendor> q, string? sortBy, string? sortDir)
    {
        bool desc = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);
        return (sortBy?.ToUpperInvariant()) switch
        {
            "CODE" => desc ? q.OrderByDescending(v => v.Code) : q.OrderBy(v => v.Code),
            "STATUS" => desc ? q.OrderByDescending(v => v.Status) : q.OrderBy(v => v.Status),
            "CREATEDUTC" => desc ? q.OrderByDescending(v => v.CreatedUtc) : q.OrderBy(v => v.CreatedUtc),
            _ => desc ? q.OrderByDescending(v => v.Name) : q.OrderBy(v => v.Name),
        };
    }
}
