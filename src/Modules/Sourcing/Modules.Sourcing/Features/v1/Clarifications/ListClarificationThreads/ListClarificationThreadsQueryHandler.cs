using FSH.Framework.Core.Context;
using FSH.Framework.Core.Exceptions;
using FSH.Modules.Identity.Contracts.Services;
using FSH.Modules.Sourcing.Contracts.Authorization;
using FSH.Modules.Sourcing.Contracts.Dtos;
using FSH.Modules.Sourcing.Contracts.v1.Clarifications;
using FSH.Modules.Sourcing.Data;
using FSH.Modules.Sourcing.Domain;
using FSH.Modules.Sourcing.Services;
using FSH.Modules.Suppliers.Contracts.v1.Vendors;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Sourcing.Features.v1.Clarifications.ListClarificationThreads;

/// <summary>
/// Dual-purpose: a caller with a vendorId claim only ever sees their own threads (no permission
/// check needed); an internal caller must hold <see cref="SourcingPermissions.Clarifications.View"/>,
/// checked here rather than via <c>.RequirePermission()</c> since this one endpoint serves both.
/// </summary>
public sealed class ListClarificationThreadsQueryHandler(
    SourcingDbContext dbContext,
    ICurrentUser currentUser,
    IUserService userService,
    IMediator mediator)
    : IQueryHandler<ListClarificationThreadsQuery, IReadOnlyList<ClarificationThreadDto>>
{
    public async ValueTask<IReadOnlyList<ClarificationThreadDto>> Handle(ListClarificationThreadsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var vendorId = currentUser.GetVendorId();
        var messages = dbContext.Clarifications.AsNoTracking().AsQueryable();

        if (vendorId is { } ownVendorId)
        {
            messages = messages.Where(c => c.VendorId == ownVendorId);
        }
        else
        {
            bool allowed = await userService
                .HasPermissionAsync(currentUser.GetUserId().ToString(), SourcingPermissions.Clarifications.View, cancellationToken)
                .ConfigureAwait(false);
            if (!allowed)
            {
                throw new ForbiddenException("Clarifications view permission is required.");
            }
        }

        var all = await messages.ToListAsync(cancellationToken).ConfigureAwait(false);

        var threads = all
            .GroupBy(c => (c.Scope, c.VendorId))
            .Select(g => new
            {
                g.Key.Scope,
                g.Key.VendorId,
                Count = g.Count(),
                Last = g.Max(c => c.CreatedUtc),
                HasUnread = vendorId is not null ? g.Any(c => !c.ReadByVendor) : g.Any(c => !c.ReadByBuyer),
            })
            .OrderByDescending(t => t.Last)
            .ToList();

        var result = new List<ClarificationThreadDto>();
        foreach (var thread in threads)
        {
            string? vendorName = null;
            string? vendorCode = null;
            try
            {
                var vendor = await mediator.Send(new GetVendorByIdQuery(thread.VendorId), cancellationToken).ConfigureAwait(false);
                vendorName = vendor.Name;
                vendorCode = vendor.Code;
            }
            catch (NotFoundException)
            {
                vendorName = "(unknown vendor)";
            }

            result.Add(new ClarificationThreadDto(thread.Scope, thread.VendorId, vendorName, vendorCode, thread.Count, thread.Last, thread.HasUnread));
        }

        return result;
    }
}
