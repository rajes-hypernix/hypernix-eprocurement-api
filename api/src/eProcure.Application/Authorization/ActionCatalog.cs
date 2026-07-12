using eProcure.Domain.Identity;

namespace eProcure.Application.Authorization;

/// <summary>
/// THE single declarative action → allowed-roles catalog, mirroring
/// docs/AUTHORIZATION-MATRIX.md row for row (as ruled 2026-07-11). The web's
/// gating map is DERIVED from this at runtime (GET /api/auth/permissions);
/// nothing else may define who-may-do-what. Anything not granted here is denied
/// (operator's general rule: implicit → deny). Service-layer checks (SoD,
/// evaluator assignment, vendor resource scoping) remain the fine-grained layer
/// UNDER this coarse role gate — this catalog replaces none of them.
/// </summary>
public static class ActionCatalog
{
    private const string B = Roles.Buyer;
    private const string Ap = Roles.Approver;
    private const string TE = Roles.TechEvaluator;
    private const string CE = Roles.CommEvaluator;
    private const string Ad = Roles.Admin;
    private const string V = Roles.Vendor;

    /// <summary>Action → roles allowed to perform it. One entry per matrix row.</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Rules =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            // ---- A1–A6 all-principal ----
            [ApiActions.ViewPersonas] = [B, Ap, TE, CE, Ad, V],           // A2
            [ApiActions.ViewPermissions] = [B, Ap, TE, CE, Ad, V],        // A58 (Phase 3: every principal reads its own action list)
            [ApiActions.Search] = [B, Ap, TE, CE, Ad, V],                 // A3 (OD-4: result-type filter in SearchService)
            [ApiActions.DownloadFile] = [B, Ap, TE, CE, Ad, V],           // A4 (FileAccessPolicy scopes vendors)
            [ApiActions.UploadFile] = [B, V],                             // A5 (OD-5)
            [ApiActions.ViewCustomLists] = [B, Ap, TE, CE, Ad, V],        // A6

            // ---- A7–A23 internal reads ----
            [ApiActions.ViewRequisitions] = [B, Ap, Ad],                  // A7 (OD-1/OD-2)
            [ApiActions.ViewRfqs] = [B, Ap, TE, CE, Ad, V],               // A8 (vendor list scoping in service)
            [ApiActions.ViewBidOpenings] = [B, Ap, TE, CE, Ad],           // A9
            [ApiActions.ViewTechnicalEval] = [B, Ap, TE, CE, Ad],         // A10 (alias masking stays in service)
            [ApiActions.ViewAwards] = [B, Ap, Ad],                        // A11
            [ApiActions.ViewPos] = [B, Ap, Ad, V],                        // A12
            [ApiActions.ViewAsns] = [B, Ap, Ad, V],                       // A13
            [ApiActions.ViewInvoices] = [B, Ap, Ad, V],                   // A14
            [ApiActions.ViewStatements] = [B, Ap, Ad],                    // A15 (vendors use ViewMyStatement)
            [ApiActions.ViewVendors] = [B, Ap, Ad, V],                    // A16 (V test-pinned, bank masked; TE/CE denied per OD-2 — alias masking end-to-end)
            [ApiActions.ViewAuditTrail] = [B, Ap, Ad],                    // A17 (OD-10)
            [ApiActions.ViewForms] = [B, Ap, Ad],                         // A18
            [ApiActions.ViewSwecTaxonomy] = [B, Ap, Ad],                  // A19
            [ApiActions.ViewOnboarding] = [B, Ap, Ad],                    // A20
            [ApiActions.ViewClarifications] = [B, Ap, TE, CE, Ad, V],     // A21
            [ApiActions.ViewUsers] = [B, Ap, TE, CE, Ad],                 // A22 (OD-6)
            [ApiActions.ViewVendorLogins] = [Ad],                         // A23

            // ---- A24–A35 internal writes: sourcing ----
            [ApiActions.ManageRequisitions] = [B],                        // A24
            [ApiActions.ManageRfqDraft] = [B],                            // A25
            [ApiActions.ManageRfqLifecycle] = [B],                        // A26
            [ApiActions.InviteVendorToRfq] = [B],                         // A27 (REG row 1; covers re-invite T8)
            [ApiActions.RescindRfqInvitation] = [B],                      // A28 (REG row 2)
            [ApiActions.ExtendRfq] = [B],                                 // A29 (REG row 3)
            [ApiActions.OpenTechnicalEnvelope] = [TE],                    // A30 (assignment check stays in service)
            [ApiActions.OpenCommercialEnvelope] = [B, CE],                // A31 (dual: assigned CE; single: Buyer)
            [ApiActions.ScoreTechnical] = [TE],                           // A32
            [ApiActions.FinalizeTechnical] = [B, TE],                     // A33 (OD-8)
            [ApiActions.SubmitAward] = [B],                               // A34
            [ApiActions.ApproveAward] = [Ap],                             // A35 (mirrors AwardService DoA + SoD)

            // ---- A36–A48 internal writes: P2P / master data / setup / admin ----
            [ApiActions.IssuePo] = [B],                                   // A36
            [ApiActions.ReceiveGoods] = [B],                              // A37
            [ApiActions.ApproveInvoice] = [B],                            // A38 (OD-7; PV/DoA supersedes at L5)
            [ApiActions.ResolveInvoiceException] = [B],                   // A39 (OD-7)
            [ApiActions.ManageVendors] = [B],                             // A40
            [ApiActions.ToggleVendorStatus] = [B, Ad],                    // A41 (OD-3: master-data governance)
            [ApiActions.InviteOnboarding] = [B],                          // A42
            [ApiActions.RevokeOnboardingInvitation] = [B],                // A43 (REG row 9)
            [ApiActions.ReviewOnboardingApplication] = [B],               // A44
            [ApiActions.ManageForms] = [B, Ad],                           // A45 (OD-3: forms are administrative)
            [ApiActions.ManageCustomLists] = [Ad],                        // A46
            [ApiActions.ManageUsers] = [Ad],                              // A47
            [ApiActions.SendClarification] = [B, TE, CE, V],              // A48 (REG row 10; Ap/Ad deny-by-default)

            // ---- A59–A61 saved views engine (D3, ruled) ----
            [ApiActions.UseSavedViews] = [B, Ap, TE, CE, Ad, V],          // A59: list/fields/run; run ALSO checks the record type's View* (ViewVocabulary.ViewActionFor)
            [ApiActions.ManageOwnSavedViews] = [B, Ap, TE, CE, Ad, V],    // A60: own views only — the scoped sources make vendor views safe
            [ApiActions.ManageSharedViews] = [B, Ad],                     // A61: sharing is publication; carried by POST /api/views/{id}/share

            // ---- A62–A64 dashboards + metric layer (D4, ruled) ----
            [ApiActions.UseDashboards] = [B, Ap, TE, CE, Ad, V],          // A62: mine/metrics; metric reads ALSO check the metric's RequiredAction (SystemMetricService)
            [ApiActions.ManageOwnDashboard] = [B, Ap, TE, CE, Ad, V],     // A63: personalize/arrange/reset own copy (copy-on-write)
            [ApiActions.ManageRoleDashboards] = [Ad],                     // A64: role defaults are platform configuration (OD-3 posture), not publication

            // ---- A65–A67 custom fields (D5, ruled) ----
            [ApiActions.ManageCustomFields] = [Ad],                       // A65: defs are platform configuration (OD-3 posture)
            [ApiActions.ReadCustomValues] = [B, Ap, TE, CE, Ad, V],       // A66: + dynamic View* + the record's scoped fetch (vendors read only records they can reach)
            [ApiActions.EditCustomValues] = [B],                          // A67: Buyer only (ruled deny-by-default: Admin per OD-3, vendors pending a concrete need)

            // ---- A68 custom segments (D6, ruled) ----
            [ApiActions.ManageSegments] = [Ad],                           // A68: defs/values/applications are platform configuration; assignments ride A66/A67 (folded — split only if the role sets ever diverge)

            // ---- A69–A71 entry forms + numbering (D7, ruled OD-D7-7) ----
            [ApiActions.ManageEntryForms] = [Ad],                         // A69: form definitions are platform configuration (A65/A68 posture)
            [ApiActions.ManageNumbering] = [Ad],                          // A70: numbering schemes likewise; reads ride the manage row (A65 precedent)
            [ApiActions.ReadEntryForms] = [B, Ap, TE, CE, Ad, V],         // A71: every principal resolves ITS OWN form; + dynamic record-type View* inside (4th use of the convention)

            // ---- A49–A57 vendor principal ----
            [ApiActions.ViewMyInvitations] = [V],                         // A49
            [ApiActions.DeclineRfqInvitation] = [V],                      // A50 (REG row 5)
            [ApiActions.DeclareIntendToBid] = [V],                        // A51 (REG row 6)
            [ApiActions.WithdrawBid] = [V],                               // A52 (REG row 7)
            [ApiActions.SubmitBid] = [V],                                 // A53
            [ApiActions.AcknowledgePo] = [V],                             // A54 (OD-9: vendor only — probity)
            [ApiActions.CreateAsn] = [V],                                 // A55 (OD-9)
            [ApiActions.SubmitInvoice] = [V],                             // A56 (OD-9)
            [ApiActions.ViewMyStatement] = [V],                           // A57
        };

    /// <summary>
    /// Roles allowed for <paramref name="action"/>; EMPTY for an unknown action —
    /// the policy provider turns empty into deny-all (fail-closed), so a typo'd or
    /// unlisted action name can never open an endpoint.
    /// </summary>
    public static IReadOnlyList<string> RolesFor(string action) =>
        Rules.TryGetValue(action, out var roles) ? roles : [];

    /// <summary>The caller's allowed actions, derived from role claims (Phase 3 permissions endpoint).</summary>
    public static IReadOnlyList<string> ActionsFor(IEnumerable<string> roles)
    {
        var set = roles.ToHashSet(StringComparer.Ordinal);
        return Rules.Where(kv => kv.Value.Any(set.Contains)).Select(kv => kv.Key).OrderBy(a => a, StringComparer.Ordinal).ToList();
    }
}
