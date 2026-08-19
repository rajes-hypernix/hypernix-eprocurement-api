using FSH.Modules.Sourcing.Contracts.Dtos;
using FSH.Modules.Sourcing.Domain;

namespace FSH.Modules.Sourcing.Features.v1.Rfqs;

internal static class RfqDtoMapper
{
    internal static RfqListItemDto ToListItemDto(Rfq rfq, int bidCount = 0)
    {
        ArgumentNullException.ThrowIfNull(rfq);
        return new RfqListItemDto(
            rfq.Id,
            rfq.Code,
            rfq.Title,
            rfq.Envelope.ToString(),
            rfq.Status.ToString(),
            rfq.Currency,
            rfq.ClosesUtc,
            rfq.Invitations.Count,
            rfq.Lines.Count,
            bidCount);
    }

    internal static RfqDetailDto ToDetailDto(
        Rfq rfq,
        IReadOnlyDictionary<Guid, (string Name, string Code)> vendorLookup,
        string baseCurrency = "MYR")
    {
        ArgumentNullException.ThrowIfNull(rfq);
        ArgumentNullException.ThrowIfNull(vendorLookup);

        return new RfqDetailDto(
            rfq.Id,
            rfq.Code,
            rfq.Title,
            rfq.Envelope.ToString(),
            rfq.Status.ToString(),
            rfq.Currency,
            rfq.ExchangeRateToBase,
            string.IsNullOrWhiteSpace(baseCurrency) ? "MYR" : baseCurrency,
            rfq.OwnerUserId,
            rfq.OpensUtc,
            rfq.ClosesUtc,
            rfq.OriginalClosesUtc,
            rfq.ExtensionCount,
            rfq.ReleasedUtc,
            rfq.ClosedUtc,
            rfq.PrRefs,
            [.. rfq.Lines.Select(l => new RfqLineDto(l.LineCode, l.ItemCode, l.Description, l.Qty, l.Uom, l.PrRef, l.SourcePrLineIds))],
            [.. rfq.FormItems.Select(f => new FormItemDto(f.Kind, f.Group, f.Section, f.Label, f.Type, f.Required, f.ConfigJson, f.Help, f.Order))],
            rfq.TechnicalSections,
            rfq.CommercialSections,
            rfq.TechnicalEvaluatorIds,
            rfq.CommercialEvaluatorIds,
            rfq.TechnicalOpened,
            rfq.TechFinalized,
            rfq.CommercialOpened,
            [.. rfq.Invitations.Select(i => ToInvitationDto(i, vendorLookup))],
            [.. rfq.Events.OrderBy(e => e.OccurredUtc).Select(ToEventDto)],
            rfq.CreatedUtc,
            rfq.UpdatedUtc,
            rfq.ClarificationDeadlineUtc,
            rfq.BidValidityDays,
            rfq.PartialBidsAllowed,
            rfq.IncotermId,
            rfq.IncotermCode,
            rfq.IncotermSuffix,
            ComposeIncoterm(rfq.IncotermCode, rfq.IncotermSuffix));
    }

    private static RfqInvitationDto ToInvitationDto(RfqInvitation invitation, IReadOnlyDictionary<Guid, (string Name, string Code)> vendorLookup)
    {
        vendorLookup.TryGetValue(invitation.VendorId, out var vendor);
        return new RfqInvitationDto(
            invitation.Id,
            invitation.VendorId,
            vendor.Name,
            vendor.Code,
            invitation.RoundNumber,
            invitation.Status.ToString(),
            invitation.DeclineReasonCode,
            invitation.DeclineNote,
            invitation.RescindReasonCode,
            invitation.RescindNote,
            invitation.InvitedUtc,
            invitation.ViewedUtc,
            invitation.RespondedUtc,
            invitation.RescindedUtc);
    }

    private static RfqEventDto ToEventDto(RfqEvent evt) =>
        new(evt.Id, evt.EventType.ToString(), evt.VendorId, evt.ActorUserId, evt.ReasonCode, evt.ReasonNote, evt.OldClosesUtc, evt.NewClosesUtc, evt.OccurredUtc);

    private static string? ComposeIncoterm(string? code, string? suffix)
    {
        var c = string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();
        var s = string.IsNullOrWhiteSpace(suffix) ? null : suffix.Trim();
        if (c is null && s is null) return null;
        if (c is null) return s;
        return s is null ? c : $"{c} {s}";
    }
}
