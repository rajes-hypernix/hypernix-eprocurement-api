using eProcure.Application;
using eProcure.Application.Abstractions;
using eProcure.Application.Authorization;
using eProcure.Application.Onboarding;
using eProcure.Application.Procurement;
using eProcure.Application.Sourcing;
using eProcure.Application.Suppliers;
using eProcure.Application.Views;
using eProcure.Domain.Views;

namespace eProcure.Infrastructure.Services;

/// <summary>The one reachability guard (A2F-T4) — body byte-identical to the D5/D6 twins
/// it replaces; the existing D3–D7 suites are the behaviour net.</summary>
public sealed class RecordReachability(
    ICurrentUser user,
    IRequisitionService requisitions,
    IRfqService rfqs,
    IPoService pos,
    IInvoiceService invoices,
    IDeliveryService deliveries,
    IVendorService vendors,
    IOnboardingService onboarding) : IRecordReachability
{
    public async Task RequireReachableAsync(RecordType type, Guid recordId, CancellationToken ct = default)
    {
        var action = ViewVocabulary.ViewActionFor[type];
        if (!ActionCatalog.RolesFor(action).Any(user.Roles.Contains))
            throw new ForbiddenException("Not permitted for your role.");

        var exists = type switch
        {
            RecordType.Requisition => await requisitions.GetAsync(recordId, ct) is not null,
            RecordType.Rfq => await rfqs.GetAsync(recordId, ct) is not null,          // live-invitation guard rides along
            RecordType.PurchaseOrder => await pos.GetAsync(recordId, ct) is not null, // EnsureCanAccess rides along
            RecordType.Invoice => await invoices.GetAsync(recordId, ct) is not null,
            RecordType.Asn => await deliveries.GetAsync(recordId, ct) is not null,
            RecordType.Vendor => await vendors.GetAsync(recordId, ct) is not null,
            RecordType.Onboarding => await onboarding.GetApplicationAsync(recordId, ct) is not null,
            RecordType.Grn => await deliveries.GrnReachableAsync(recordId, ct),   // CF-FIX4-T2
            _ => false,
        };
        if (!exists) throw new NotFoundException($"{type} {recordId} not found.");
    }
}
