using eProcure.Application.Authorization;
using eProcure.Domain.Onboarding;
using eProcure.Domain.Procurement;
using eProcure.Domain.Sourcing;
using eProcure.Domain.Suppliers;
using eProcure.Domain.Views;

namespace eProcure.Application.Views;

/// <summary>
/// The maps that sit BESIDE the catalog and the registry (D3 Step 0(c)/(b), ruled),
/// commented per row like everything else. One file, no second source.
/// </summary>
public static class ViewVocabulary
{
    /// <summary>
    /// RecordType → the View* action running a view of that type requires. The run handler
    /// checks this dynamically on top of its static [Action(UseSavedViews)] — a principal
    /// without the record type's read action gets 403 (e.g. TechEvaluator × Vendor view).
    /// </summary>
    public static readonly IReadOnlyDictionary<RecordType, string> ViewActionFor = new Dictionary<RecordType, string>
    {
        [RecordType.Requisition] = ApiActions.ViewRequisitions,    // internal-only per matrix A7
        [RecordType.Rfq] = ApiActions.ViewRfqs,                    // vendor allowed, invitation-scoped source (A8)
        [RecordType.PurchaseOrder] = ApiActions.ViewPos,           // vendor allowed, VendorId-scoped source (A12)
        [RecordType.Invoice] = ApiActions.ViewInvoices,            // vendor allowed, VendorId-scoped source (A14)
        [RecordType.Asn] = ApiActions.ViewAsns,                    // vendor allowed, VendorId-scoped source (A13)
        [RecordType.Vendor] = ApiActions.ViewVendors,              // vendor allowed; list DTO carries no bank fields (A16)
        [RecordType.Onboarding] = ApiActions.ViewOnboarding,       // internal-only per matrix A20
    };

    /// <summary>
    /// Enum-typed registry fields → their option lists, from the DOMAIN enums (code, not
    /// stored blobs — typed core holds). Stringly-typed display values (Vendor.Type/Region)
    /// are enumerated explicitly from the seeded vocabulary.
    /// </summary>
    public static readonly IReadOnlyDictionary<(RecordType, string), string[]> EnumOptions =
        new Dictionary<(RecordType, string), string[]>
        {
            [(RecordType.Requisition, "HeaderStatus")] = Enum.GetNames<PrHeaderStatus>(),
            [(RecordType.Rfq, "Envelope")] = Enum.GetNames<RfqEnvelope>(),
            [(RecordType.Rfq, "Status")] = Enum.GetNames<RfqStatus>(),
            [(RecordType.PurchaseOrder, "Status")] = Enum.GetNames<PoStatus>(),
            [(RecordType.Invoice, "Status")] = Enum.GetNames<InvoiceStatus>(),
            [(RecordType.Invoice, "MatchStatus")] = ["Matched", "Variance"],               // InvoiceListDto.MatchStatus display values
            [(RecordType.Asn, "Status")] = Enum.GetNames<AsnStatus>(),
            [(RecordType.Vendor, "Type")] = ["SWEC", "Non-SWEC"],                          // VendorService.TypeDisplay
            [(RecordType.Vendor, "Region")] = ["Peninsular", "Sarawak", "Sabah"],          // VendorMaster filter vocabulary
            [(RecordType.Vendor, "Status")] = Enum.GetNames<VendorStatus>(),
            [(RecordType.Onboarding, "Type")] = ["SWEC", "Non-SWEC"],
            [(RecordType.Onboarding, "Status")] = Enum.GetNames<OnboardingStatus>(),
            [(RecordType.Onboarding, "Source")] = Enum.GetNames<ApplicationSource>(),
        };

    /// <summary>The ruled relative-date value tokens — gate-driven set, no token without a
    /// consumer (same rule as operators). Resolved by the executor at run time.</summary>
    public static readonly IReadOnlyList<string> DateTokens = ["@today", "@startOfMonth", "@endOfMonth"];
}
