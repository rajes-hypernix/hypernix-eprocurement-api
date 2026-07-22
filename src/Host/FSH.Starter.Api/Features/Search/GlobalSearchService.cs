using FSH.Framework.Core.Context;
using FSH.Modules.Identity.Contracts.Services;
using FSH.Modules.Platform.Contracts.Dtos;
using FSH.Modules.Platform.Contracts.v1.Search;
using FSH.Modules.Procurement.Contracts.Authorization;
using FSH.Modules.Procurement.Data;
using FSH.Modules.Sourcing.Contracts.Authorization;
using FSH.Modules.Sourcing.Data;
using FSH.Modules.Suppliers.Contracts.Authorization;
using FSH.Modules.Suppliers.Data;
using Microsoft.EntityFrameworkCore;

namespace FSH.Starter.Api.Features.Search;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Instantiated by DI via AddScoped")]
internal sealed class GlobalSearchService(
    SuppliersDbContext suppliers,
    SourcingDbContext sourcing,
    ProcurementDbContext procurement,
    ICurrentUser currentUser,
    IUserPermissionService permissions)
{
    private const int Cap = 8;

    public async Task<IReadOnlyList<SearchHitDto>> SearchAsync(string? q, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
            return [];

        string term = q.Trim();
        string pattern = $"%{term}%";
        var userId = currentUser.GetUserId().ToString();
        var granted = await permissions.GetPermissionsAsync(userId, cancellationToken).ConfigureAwait(false) ?? [];
        bool Has(string permission) => granted.Contains(permission);

        Guid? vendorId = currentUser.GetVendorId();
        var hits = new List<SearchHitDto>();

        if (Has(SuppliersPermissions.Vendors.View))
            hits.AddRange(await SearchVendorsAsync(pattern, vendorId, cancellationToken).ConfigureAwait(false));

        if (vendorId is null && Has(SourcingPermissions.Requisitions.View))
            hits.AddRange(await SearchRequisitionsAsync(pattern, cancellationToken).ConfigureAwait(false));

        if (Has(SourcingPermissions.Rfqs.View))
            hits.AddRange(await SearchRfqsAsync(pattern, vendorId, cancellationToken).ConfigureAwait(false));

        if (Has(ProcurementPermissions.PurchaseOrders.View))
            hits.AddRange(await SearchPurchaseOrdersAsync(pattern, vendorId, cancellationToken).ConfigureAwait(false));

        if (Has(ProcurementPermissions.Invoices.View))
            hits.AddRange(await SearchInvoicesAsync(pattern, vendorId, cancellationToken).ConfigureAwait(false));

        if (Has(ProcurementPermissions.Deliveries.View))
            hits.AddRange(await SearchAsnsAsync(pattern, vendorId, cancellationToken).ConfigureAwait(false));

        if (vendorId is null && Has(ProcurementPermissions.Statements.View))
            hits.AddRange(await SearchStatementsAsync(pattern, cancellationToken).ConfigureAwait(false));

        return hits;
    }

    private async Task<IReadOnlyList<SearchHitDto>> SearchVendorsAsync(
        string pattern,
        Guid? vendorId,
        CancellationToken cancellationToken)
    {
        var q = suppliers.Vendors.AsNoTracking();

        if (vendorId is { } vid)
        {
            q = q.Where(v => v.Id == vid);
        }
        else
        {
            q = q.Where(v =>
                EF.Functions.ILike(v.Code, pattern) ||
                EF.Functions.ILike(v.Name, pattern) ||
                EF.Functions.ILike(v.RegisteredName, pattern));
        }

        return await q
            .OrderBy(v => v.Code)
            .Take(Cap)
            .Select(v => new SearchHitDto(SearchHitType.Vendor, v.Id, v.Code, v.Name, v.RegisteredName))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<SearchHitDto>> SearchRequisitionsAsync(
        string pattern,
        CancellationToken cancellationToken) =>
        await sourcing.PurchaseRequisitions
            .AsNoTracking()
            .Where(r =>
                EF.Functions.ILike(r.Code, pattern) ||
                EF.Functions.ILike(r.Requestor, pattern) ||
                EF.Functions.ILike(r.Memo, pattern) ||
                EF.Functions.ILike(r.Department, pattern))
            .OrderByDescending(r => r.UpdatedUtc)
            .Take(Cap)
            .Select(r => new SearchHitDto(
                SearchHitType.Requisition,
                r.Id,
                r.Code,
                r.Requestor,
                r.Department))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    private async Task<IReadOnlyList<SearchHitDto>> SearchRfqsAsync(
        string pattern,
        Guid? vendorId,
        CancellationToken cancellationToken)
    {
        var q = sourcing.Rfqs.AsNoTracking();

        if (vendorId is { } vid)
        {
            var invitedIds = sourcing.RfqInvitations
                .AsNoTracking()
                .Where(i => i.VendorId == vid)
                .Select(i => i.RfqId);
            q = q.Where(r => invitedIds.Contains(r.Id));
        }

        q = q.Where(r =>
            EF.Functions.ILike(r.Code, pattern) ||
            EF.Functions.ILike(r.Title, pattern));

        return await q
            .OrderByDescending(r => r.UpdatedUtc)
            .Take(Cap)
            .Select(r => new SearchHitDto(SearchHitType.Rfq, r.Id, r.Code, r.Title, r.Status.ToString()))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<SearchHitDto>> SearchPurchaseOrdersAsync(
        string pattern,
        Guid? vendorId,
        CancellationToken cancellationToken)
    {
        var q = procurement.PurchaseOrders.AsNoTracking();

        if (vendorId is { } vid)
            q = q.Where(p => p.VendorId == vid);

        q = q.Where(p => EF.Functions.ILike(p.Code, pattern));

        return await q
            .OrderByDescending(p => p.UpdatedUtc)
            .Take(Cap)
            .Select(p => new SearchHitDto(
                SearchHitType.PurchaseOrder,
                p.Id,
                p.Code,
                p.Status.ToString(),
                null))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<SearchHitDto>> SearchInvoicesAsync(
        string pattern,
        Guid? vendorId,
        CancellationToken cancellationToken)
    {
        var q = procurement.Invoices.AsNoTracking();

        if (vendorId is { } vid)
        {
            var poIds = procurement.PurchaseOrders
                .AsNoTracking()
                .Where(p => p.VendorId == vid)
                .Select(p => p.Id);
            q = q.Where(i => poIds.Contains(i.PoId));
        }

        q = q.Where(i =>
            EF.Functions.ILike(i.Code, pattern) ||
            EF.Functions.ILike(i.InvoiceNo, pattern));

        return await q
            .OrderByDescending(i => i.CreatedUtc)
            .Take(Cap)
            .Select(i => new SearchHitDto(
                SearchHitType.Invoice,
                i.Id,
                i.Code,
                i.InvoiceNo,
                i.Status.ToString()))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<SearchHitDto>> SearchAsnsAsync(
        string pattern,
        Guid? vendorId,
        CancellationToken cancellationToken)
    {
        var q = procurement.Asns.AsNoTracking();

        if (vendorId is { } vid)
        {
            var poIds = procurement.PurchaseOrders
                .AsNoTracking()
                .Where(p => p.VendorId == vid)
                .Select(p => p.Id);
            q = q.Where(a => poIds.Contains(a.PoId));
        }

        q = q.Where(a =>
            EF.Functions.ILike(a.Code, pattern) ||
            EF.Functions.ILike(a.TrackingNo, pattern) ||
            EF.Functions.ILike(a.Carrier, pattern));

        return await q
            .OrderByDescending(a => a.CreatedUtc)
            .Take(Cap)
            .Select(a => new SearchHitDto(
                SearchHitType.Asn,
                a.Id,
                a.Code,
                a.TrackingNo,
                a.Status.ToString()))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<SearchHitDto>> SearchStatementsAsync(
        string pattern,
        CancellationToken cancellationToken) =>
        await suppliers.Vendors
            .AsNoTracking()
            .Where(v =>
                EF.Functions.ILike(v.Code, pattern) ||
                EF.Functions.ILike(v.Name, pattern) ||
                EF.Functions.ILike(v.RegisteredName, pattern))
            .OrderBy(v => v.Code)
            .Take(Cap)
            .Select(v => new SearchHitDto(
                SearchHitType.Statement,
                v.Id,
                v.Code,
                v.Name,
                "Statement of account"))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}
