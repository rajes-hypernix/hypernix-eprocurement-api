using eProcure.Application.Sourcing;
using eProcure.Domain.Sourcing;

namespace eProcure.Infrastructure.Services;

/// <summary>Entity ⇄ DTO mapping for sourcing aggregates.</summary>
internal static class SourcingMapping
{
    public static RequisitionDto ToDto(PurchaseRequisition p, IReadOnlySet<Guid>? noQuoteLineIds = null) => new(
        p.Id, p.Code, p.Requestor, p.Department, p.Location, p.Memo, p.Job, p.Category,
        p.CostCentre, p.Project, p.RaisedOn, p.RequiredOn, p.Status, p.DerivedValue,
        p.Lines.Select(l => new PrLineDto(
            l.Id, l.ItemCode, l.Description, l.Qty, l.Uom, l.EstUnitPrice,
            LegacyToken(l.LifecycleStatus), l.Ref, l.LifecycleStatus.ToString(),
            NoQuotes: noQuoteLineIds?.Contains(l.Id) ?? false,
            Editable: l.LifecycleStatus == PrLineStatus.Open)).ToList(),
        p.HeaderStatus.ToString(), p.Submitted, p.EntryFormId);

    /// <summary>Controlled analytics dimension code from a display label (conformed dimension,
    /// DATA-MODEL-ANALYTICS §4) — shared by the seed and PR create/edit so codes stay stable.</summary>
    public static string DimCode(string display)
    {
        var s = new string(display.ToUpperInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
        return s.Length > 40 ? s[..40] : s;
    }

    /// <summary>Projects the typed <see cref="PrLineStatus"/> to the legacy string token
    /// ("available" | "rfq" | "awarded" | …) that existing readers/UI consume unchanged.</summary>
    public static string LegacyToken(PrLineStatus s) => s switch
    {
        PrLineStatus.Open => "available",
        PrLineStatus.InDraftRfq => "rfq",   // reserved → shown as locked, not re-sourceable
        PrLineStatus.InRfq => "rfq",
        PrLineStatus.Awarded => "awarded",
        PrLineStatus.Cancelled => "cancelled",
        PrLineStatus.Closed => "closed",
        _ => "available",
    };

    /// <summary>Maps a legacy seed/import token to the typed lifecycle (analytics §7).</summary>
    public static PrLineStatus FromLegacyToken(string token) => token switch
    {
        "available" => PrLineStatus.Open,
        "rfq" => PrLineStatus.InRfq,
        "awarded" => PrLineStatus.Awarded,
        "cancelled" => PrLineStatus.Cancelled,
        "closed" => PrLineStatus.Closed,
        _ => PrLineStatus.Open,
    };

    public static FormItemDto ToDto(FormItem i) =>
        new(i.Kind, i.Group, i.Section, i.Label, i.Type, i.Required, i.ConfigJson, i.Help, i.Order);

    public static FormItem ToEntity(FormItemDto d, int order) => new()
    {
        Kind = d.Kind, Group = d.Group, Section = d.Section, Label = d.Label, Type = d.Type,
        Required = d.Required, ConfigJson = string.IsNullOrWhiteSpace(d.Config) ? "{}" : d.Config,
        Help = d.Help, Order = order,
    };

    public static RfqLine ToEntity(RfqLineDto d) => new()
    {
        // LineCode is the stable lineage target; one line per item in v1 → default to the item code.
        LineCode = d.ItemCode, ItemCode = d.ItemCode, Description = d.Description, Qty = d.Qty, Uom = d.Uom, PrRef = d.PrRef,
        SourcePrLineIds = d.SourcePrLineIds?.ToList() ?? [],
    };

    /// <summary>Live invited vendor ids — the non-Rescinded invitations. This is the replacement for the
    /// retired InvitedVendorIds delimited column and preserves its exact vendor-set semantics for every
    /// existing consumer (README rule 1; RFQ-LIFECYCLE-ADDENDUM §6). Requires <c>r.Invitations</c> loaded.</summary>
    public static List<string> LiveInvitedVendorIds(Rfq r) => r.Invitations
        .Where(i => i.Status != RfqInvitationStatus.Rescinded)
        .OrderBy(i => i.InvitedUtc).ThenBy(i => i.VendorId)   // deterministic order → stable masked aliases
        .Select(i => i.VendorId.ToString()).ToList();

    public static RfqDetail ToDetail(
        Rfq r,
        IReadOnlyList<RfqInvitedVendorDto>? invited = null,
        IReadOnlyList<RfqInvitationDto>? invitations = null,
        IReadOnlyList<RfqEventDto>? events = null,
        int extensionCount = 0,
        int maxExtensions = 0) => new(
        r.Id, r.Code, r.Title, r.Envelope.ToString(), r.Status.ToString(), r.Currency,
        r.OpensUtc, r.ClosesUtc, r.PrRefs, LiveInvitedVendorIds(r), r.TechnicalEvaluatorIds, r.CommercialEvaluatorIds,
        r.TechFinalized, r.CommercialOpened,
        r.Lines.Select(l => new RfqLineDto(l.ItemCode, l.Description, l.Qty, l.Uom, l.PrRef, l.SourcePrLineIds)).ToList(),
        r.FormItems.OrderBy(i => i.Order).Select(ToDto).ToList(),
        r.TechnicalSections, r.CommercialSections, invited ?? [],
        r.OriginalClosesUtc, extensionCount, maxExtensions, invitations ?? [], events ?? []);

    public static RfqInvitationDto ToDto(RfqInvitation i, string vendorName) => new(
        i.VendorId.ToString(), vendorName, i.Status.ToString(),
        i.DeclineReasonCode, i.DeclineNote, i.RescindReasonCode, i.RescindNote,
        i.InvitedUtc, i.ViewedUtc, i.RespondedUtc, i.RescindedUtc);

    public static RfqEventDto ToDto(RfqEvent e, string? vendorName) => new(
        e.EventType.ToString(), e.VendorId?.ToString(), vendorName,
        e.ActorUserId, e.ActorVendorUserId, e.ReasonCode, e.ReasonNote,
        e.OldClosesUtc, e.NewClosesUtc, e.OccurredUtc);

    public static RfqListItem ToListItem(Rfq r, int bidCount = 0) => new(
        r.Id, r.Code, r.Title, r.Envelope.ToString(), r.Status.ToString(), r.Currency, r.ClosesUtc,
        LiveInvitedVendorIds(r).Count, r.Lines.Count, r.FormItems.Count(i => i.Kind == "question"), bidCount);

    public static FormTemplateDto ToDto(FormTemplate f) => new(
        f.Id, f.Code, f.Name, f.Version, f.UpdatedUtc,
        f.Items.OrderBy(i => i.Order).Select(ToDto).ToList(), f.TechnicalSections, f.CommercialSections,
        f.Purpose.ToString());
}
