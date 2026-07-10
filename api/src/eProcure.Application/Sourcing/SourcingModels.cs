namespace eProcure.Application.Sourcing;

// ---- Requisitions ----
// Note: `Status` is the legacy line token (available|rfq|awarded) kept for the existing
// Confirm-lines flow; `LifecycleStatus` is the typed PR-module state (Slice A). `NoQuotes` is
// derived (an Open line whose latest sourcing link is Returned). `Editable` = the line is Open.
public sealed record PrLineDto(
    Guid Id, string ItemCode, string Description, decimal Qty, string Uom, decimal EstUnitPrice,
    string Status, string? Ref, string LifecycleStatus, bool NoQuotes, bool Editable);

public sealed record RequisitionDto(
    Guid Id, string Code, string Requestor, string Department, string Location, string Memo,
    string Job, string Category, string CostCentre, string? Project, string RaisedDate,
    string RequiredDate, string Status, decimal Value, IReadOnlyList<PrLineDto> Lines,
    string HeaderStatus, bool Submitted);

// ---- PR create / edit + line-lifecycle requests (Slice B) ----
public sealed record PrLineInput(
    Guid? Id, string ItemCode, string Description, decimal Qty, string Uom, decimal EstUnitPrice);

public sealed record SavePrRequest(
    string Requestor, string Department, string Location, string Category, string Job,
    string Memo, string RequiredDate, IReadOnlyList<PrLineInput> Lines);

public sealed record ReasonRequest(string? Reason);

public interface IRequisitionService
{
    Task<IReadOnlyList<RequisitionDto>> ListAsync(CancellationToken ct = default);
    Task<RequisitionDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<RequisitionDto> CreateAsync(SavePrRequest req, bool submit, CancellationToken ct = default);
    Task<RequisitionDto> UpdateAsync(Guid id, SavePrRequest req, CancellationToken ct = default);
    Task<RequisitionDto> SubmitAsync(Guid id, CancellationToken ct = default);   // Draft → Submitted
    Task<RequisitionDto> CancelLineAsync(Guid id, Guid lineId, string? reason, CancellationToken ct = default);
    Task<RequisitionDto> ReleaseLineAsync(Guid id, Guid lineId, string? reason, CancellationToken ct = default);
    Task<RequisitionDto> ReserveLineAsync(Guid id, Guid lineId, CancellationToken ct = default);    // Open → InDraftRfq (basket add)
    Task<RequisitionDto> UnreserveLineAsync(Guid id, Guid lineId, CancellationToken ct = default);  // InDraftRfq → Open (basket remove)
    Task<RequisitionDto> ReopenLineAsync(Guid id, Guid lineId, CancellationToken ct = default);
    Task<RequisitionDto> CancelPrAsync(Guid id, string? reason, CancellationToken ct = default);
}

// ---- Form items (shared by RFQ form + library template) ----
public sealed record FormItemDto(
    string Kind, string Group, string Section, string Label, string Type,
    bool Required, string Config, string Help, int Order);

// ---- RFQs ----
public sealed record RfqLineDto(
    string ItemCode, string Description, decimal Qty, string Uom, string? PrRef,
    IReadOnlyList<string>? SourcePrLineIds = null);   // provenance from the Consolidate basket (Slice C)

public sealed record RfqListItem(
    Guid Id, string Code, string Title, string Envelope, string Status, string Currency,
    DateTime? ClosesUtc, int InvitedCount, int LineCount, int QuestionCount, int BidCount);

public sealed record RfqDetail(
    Guid Id, string Code, string Title, string Envelope, string Status, string Currency,
    DateTime? OpensUtc, DateTime? ClosesUtc,
    IReadOnlyList<string> PrRefs,
    // NOTE: InvitedVendorIds and InvitedVendors are DERIVED from Invitations, EXCLUDING Rescinded rows,
    // so existing consumers (RfqBuilder picker, detail hub, bid tables) see the same vendor set as
    // before the delimited column was retired (RFQ-LIFECYCLE-ADDENDUM §6; README rule 1).
    IReadOnlyList<string> InvitedVendorIds,
    IReadOnlyList<string> TechnicalEvaluatorIds,
    IReadOnlyList<string> CommercialEvaluatorIds,
    bool TechFinalized, bool CommercialOpened,
    IReadOnlyList<RfqLineDto> Lines,
    IReadOnlyList<FormItemDto> FormItems,
    IReadOnlyList<string> TechnicalSections,
    IReadOnlyList<string> CommercialSections,
    IReadOnlyList<RfqInvitedVendorDto> InvitedVendors,
    DateTime? OriginalClosesUtc, int ExtensionCount,
    int MaxExtensions,                               // server governance cap (G5); the client no longer hardcodes it
    IReadOnlyList<RfqInvitationDto> Invitations,     // full invitation lifecycle incl. Rescinded (Slice J)
    IReadOnlyList<RfqEventDto> Events);              // append-only activity timeline

public sealed record RfqInvitedVendorDto(string VendorId, string VendorName, string Category, bool Submitted);

/// <summary>One vendor invitation with its full lifecycle (RFQ-LIFECYCLE-ADDENDUM §7). Rescinded rows
/// are included here (Slice J keeps them visible, struck-through).</summary>
public sealed record RfqInvitationDto(
    string VendorId, string VendorName, string Status,
    string? DeclineReasonCode, string? DeclineNote, string? RescindReasonCode, string? RescindNote,
    DateTime InvitedUtc, DateTime? ViewedUtc, DateTime? RespondedUtc, DateTime? RescindedUtc);

/// <summary>One append-only RFQ activity event, newest-first for the timeline panel.</summary>
public sealed record RfqEventDto(
    string EventType, string? VendorId, string? VendorName,
    string? ActorUserId, string? ActorVendorUserId, string? ReasonCode, string? ReasonNote,
    DateTime? OldClosesUtc, DateTime? NewClosesUtc, DateTime OccurredUtc);

// ---- Governance requests (Slice I) ----
public sealed record InviteVendorRequest(string VendorId);
public sealed record RescindInvitationRequest(string ReasonCode, string? Note);
public sealed record ExtendRfqRequest(DateTime NewClosesUtc, string ReasonCode, string? Note);
public sealed record DeclineInvitationRequest(string ReasonCode, string? Note);

public sealed record CreateRfqDraftRequest(
    string? Title, IReadOnlyList<string> PrRefs, IReadOnlyList<RfqLineDto> Lines);

public sealed record UpdateRfqDraftRequest(
    string Title, string Envelope, string Currency, DateTime? OpensUtc, DateTime? ClosesUtc,
    IReadOnlyList<RfqLineDto> Lines,
    IReadOnlyList<FormItemDto> FormItems,
    IReadOnlyList<string> TechnicalSections,
    IReadOnlyList<string> CommercialSections,
    IReadOnlyList<string> InvitedVendorIds,
    IReadOnlyList<string> TechnicalEvaluatorIds,
    IReadOnlyList<string> CommercialEvaluatorIds);

public interface IRfqService
{
    Task<IReadOnlyList<RfqListItem>> ListAsync(CancellationToken ct = default);
    Task<RfqDetail?> GetAsync(Guid id, CancellationToken ct = default);
    Task<RfqDetail> CreateDraftAsync(CreateRfqDraftRequest req, CancellationToken ct = default);
    Task<RfqDetail> UpdateDraftAsync(Guid id, UpdateRfqDraftRequest req, CancellationToken ct = default);
    Task<RfqDetail> ReleaseAsync(Guid id, CancellationToken ct = default);
    Task<RfqDetail> CloseAsync(Guid id, CancellationToken ct = default);
    Task<RfqDetail> CancelAsync(Guid id, CancellationToken ct = default);

    // Buyer governance (Slice I)
    Task<RfqDetail> InviteVendorAsync(Guid id, InviteVendorRequest req, CancellationToken ct = default);
    Task<RfqDetail> RescindInvitationAsync(Guid id, Guid vendorId, RescindInvitationRequest req, CancellationToken ct = default);
    Task<RfqDetail> ExtendAsync(Guid id, ExtendRfqRequest req, CancellationToken ct = default);
}

/// <summary>Vendor-side invitation actions (RFQ-LIFECYCLE-ADDENDUM §6). Resource-scoped to the
/// caller's own vendor via ICurrentUser; each writes an RfqEvent + AuditEntry in one transaction.</summary>
public interface IRfqVendorService
{
    Task DeclineAsync(Guid rfqId, DeclineInvitationRequest req, CancellationToken ct = default);
    Task IntendAsync(Guid rfqId, CancellationToken ct = default);          // T2 and T4 (reverse decline)
    Task WithdrawBidAsync(Guid rfqId, CancellationToken ct = default);     // T6
}

// ---- Form library ----
public sealed record FormTemplateDto(
    Guid Id, string Code, string Name, int Version, DateTime UpdatedUtc,
    IReadOnlyList<FormItemDto> Items,
    IReadOnlyList<string> TechnicalSections,
    IReadOnlyList<string> CommercialSections,
    string Purpose);

public sealed record SaveFormTemplateRequest(
    string Name,
    IReadOnlyList<FormItemDto> Items,
    IReadOnlyList<string> TechnicalSections,
    IReadOnlyList<string> CommercialSections,
    string? Purpose = null);

public interface IFormService
{
    Task<IReadOnlyList<FormTemplateDto>> ListAsync(CancellationToken ct = default);
    Task<FormTemplateDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<FormTemplateDto> CreateAsync(SaveFormTemplateRequest req, CancellationToken ct = default);
    Task<FormTemplateDto> UpdateAsync(Guid id, SaveFormTemplateRequest req, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
