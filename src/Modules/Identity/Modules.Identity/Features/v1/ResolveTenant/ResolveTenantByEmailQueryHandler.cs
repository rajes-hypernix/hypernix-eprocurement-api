using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Identity.Contracts.v1.ResolveTenant;
using FSH.Modules.Identity.Domain;
using Mediator;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FSH.Modules.Identity.Features.v1.ResolveTenant;

/// <summary>
/// Locates which tenant owns an email by scanning Identity users with query filters ignored
/// (shared-DB Finbuckle multi-tenancy). Used by login so clients do not collect a tenant code.
/// </summary>
public sealed class ResolveTenantByEmailQueryHandler(
    UserManager<FshUser> userManager,
    IMultiTenantStore<AppTenantInfo> tenantStore)
    : IQueryHandler<ResolveTenantByEmailQuery, ResolveTenantByEmailResponse>
{
    public async ValueTask<ResolveTenantByEmailResponse> Handle(
        ResolveTenantByEmailQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var normalized = query.Email.Trim().ToUpperInvariant();
        var matches = await userManager.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(u => u.NormalizedEmail == normalized && u.IsActive)
            .Select(u => new { TenantId = EF.Property<string>(u, "TenantId") })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var tenantIds = matches
            .Select(m => m.TenantId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (tenantIds.Count == 0)
        {
            return new ResolveTenantByEmailResponse(ResolveTenantStatus.NotFound, null, null, null);
        }

        var candidates = new List<ResolveTenantCandidateDto>();
        foreach (var tenantId in tenantIds)
        {
            var tenant = await tenantStore.GetAsync(tenantId).ConfigureAwait(false)
                ?? await tenantStore.GetByIdentifierAsync(tenantId).ConfigureAwait(false);

            if (tenant is null || !tenant.IsActive)
            {
                continue;
            }

            candidates.Add(new ResolveTenantCandidateDto(tenant.Id!, tenant.Name));
        }

        if (candidates.Count == 0)
        {
            return new ResolveTenantByEmailResponse(ResolveTenantStatus.NotFound, null, null, null);
        }

        if (candidates.Count == 1)
        {
            var only = candidates[0];
            return new ResolveTenantByEmailResponse(
                ResolveTenantStatus.Found,
                only.TenantId,
                only.TenantName,
                null);
        }

        return new ResolveTenantByEmailResponse(
            ResolveTenantStatus.Ambiguous,
            null,
            null,
            candidates);
    }
}
