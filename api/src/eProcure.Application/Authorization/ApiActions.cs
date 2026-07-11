namespace eProcure.Application.Authorization;

/// <summary>
/// The enumerated authorization actions — one constant per row of
/// docs/AUTHORIZATION-MATRIX.md (A1–A57, as ruled 2026-07-11). Every authenticated
/// endpoint carries exactly one of these via [Action(...)]; the role assignment
/// lives in <see cref="ActionCatalog"/>. Names here are also the wire values
/// returned by GET /api/auth/permissions (Phase 3), so renaming one is a breaking
/// change to the web's derived gating.
/// </summary>
public static class ApiActions
{
    // A1–A6 — all-principal
    public const string ViewDashboard = "ViewDashboard";
    public const string ViewPersonas = "ViewPersonas";
    public const string Search = "Search";
    public const string DownloadFile = "DownloadFile";
    public const string UploadFile = "UploadFile";
    public const string ViewCustomLists = "ViewCustomLists";

    // A7–A23 — internal reads
    public const string ViewRequisitions = "ViewRequisitions";
    public const string ViewRfqs = "ViewRfqs";
    public const string ViewBidOpenings = "ViewBidOpenings";
    public const string ViewTechnicalEval = "ViewTechnicalEval";
    public const string ViewAwards = "ViewAwards";
    public const string ViewPos = "ViewPos";
    public const string ViewAsns = "ViewAsns";
    public const string ViewInvoices = "ViewInvoices";
    public const string ViewStatements = "ViewStatements";
    public const string ViewVendors = "ViewVendors";
    public const string ViewAuditTrail = "ViewAuditTrail";
    public const string ViewForms = "ViewForms";
    public const string ViewSwecTaxonomy = "ViewSwecTaxonomy";
    public const string ViewOnboarding = "ViewOnboarding";
    public const string ViewClarifications = "ViewClarifications";
    public const string ViewUsers = "ViewUsers";
    public const string ViewVendorLogins = "ViewVendorLogins";

    // A24–A35 — internal writes, sourcing
    public const string ManageRequisitions = "ManageRequisitions";
    public const string ManageRfqDraft = "ManageRfqDraft";
    public const string ManageRfqLifecycle = "ManageRfqLifecycle";
    public const string InviteVendorToRfq = "InviteVendorToRfq";
    public const string RescindRfqInvitation = "RescindRfqInvitation";
    public const string ExtendRfq = "ExtendRfq";
    public const string OpenTechnicalEnvelope = "OpenTechnicalEnvelope";
    public const string OpenCommercialEnvelope = "OpenCommercialEnvelope";
    public const string ScoreTechnical = "ScoreTechnical";
    public const string FinalizeTechnical = "FinalizeTechnical";
    public const string SubmitAward = "SubmitAward";
    public const string ApproveAward = "ApproveAward";

    // A36–A48 — internal writes, P2P / vendor master / setup / admin
    public const string IssuePo = "IssuePo";
    public const string ReceiveGoods = "ReceiveGoods";
    public const string ApproveInvoice = "ApproveInvoice";
    public const string ResolveInvoiceException = "ResolveInvoiceException";
    public const string ManageVendors = "ManageVendors";
    public const string ToggleVendorStatus = "ToggleVendorStatus";
    public const string InviteOnboarding = "InviteOnboarding";
    public const string RevokeOnboardingInvitation = "RevokeOnboardingInvitation";
    public const string ReviewOnboardingApplication = "ReviewOnboardingApplication";
    public const string ManageForms = "ManageForms";
    public const string ManageCustomLists = "ManageCustomLists";
    public const string ManageUsers = "ManageUsers";
    public const string SendClarification = "SendClarification";

    // A49–A57 — vendor principal
    public const string ViewMyInvitations = "ViewMyInvitations";
    public const string DeclineRfqInvitation = "DeclineRfqInvitation";
    public const string DeclareIntendToBid = "DeclareIntendToBid";
    public const string WithdrawBid = "WithdrawBid";
    public const string SubmitBid = "SubmitBid";
    public const string AcknowledgePo = "AcknowledgePo";
    public const string CreateAsn = "CreateAsn";
    public const string SubmitInvoice = "SubmitInvoice";
    public const string ViewMyStatement = "ViewMyStatement";
}
