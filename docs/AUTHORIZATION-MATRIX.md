# AUTHORIZATION-MATRIX.md — Slice RM

**STATUS: RULED 2026-07-11 — the operator ruled on OD-1 … OD-10 (rulings recorded
in §6) and both scope vetoes (§7). This is the living who-may-do-what document;
enforcement (Phases 1–3) implements exactly this matrix.**

Step 0 draft produced and ruled 2026-07-11. Baselines recorded at start
(README-FIRST constraint 7):

| Gate | Baseline | How measured |
|---|---|---|
| API (`dotnet test`) | **304 / 304 green** | run at Step 0 on this machine |
| Web (`npm run test`) | **198 / 198 green** (44 files) | run at Step 0 on this machine |
| Crawl (`e2e-audit`) | **42 / 42** | as recorded at D2 close; re-run against live stack before Phase 1 and at close |

## 1. Principals and roles

Canonical role strings (`api/src/eProcure.Domain/Identity/Roles.cs:10-21`):
internal roles **Buyer, Approver, TechEvaluator, CommEvaluator, Admin**
(`Roles.Internal`); **Vendor** is reserved for VendorUser principals and must never
be granted to an internal user (SoD, enforced at `UserService.cs:34-35`).

Dev personas (`api/src/eProcure.Api/Auth/DevUserStore.cs:14-22`): u_faridah [Buyer],
u_lim [Buyer, Approver], u_hafiz / u_nur / u_raj [TechEvaluator], u_tan
[CommEvaluator], u_admin [Admin]. Vendor principals come from the `VendorUsers`
table with a single `Vendor` role claim + `vendorId` claim
(`DemoAuthenticationHandler.cs:64-75`). Phase 2 note: **no pure-Approver dev
persona exists** — the test factory provides Testing-environment principals per
role without touching the shipped persona list.

## 2. Evidence sources (cited per row)

| Key | Source |
|---|---|
| REG | `docs/PERMISSIONS-REGISTER.md` — the 10 enumerated actions (8 RFQ-lifecycle + 2 vendor-portal) |
| GATE | `web/src/ui/gating.tsx:37-54` — D2 display-gating map with provenance notes |
| NAV | `web/src/centerTabs.ts:19-77` + `web/src/App.tsx:113` — de-facto screen reach. **Load-bearing fact: the internal sidebar is NOT per-role filtered.** All six tab groups render for every internal role; the narrowing below comes from GATE + REG + service evidence + operator rulings, not from nav. |
| SVC | Existing service-layer enforcement (`ICurrentUser` checks) — cited file:line per row |
| SCREEN | Which component actually calls the endpoint — cited where it decides a question |
| TEST | Pinned behaviour that must NOT change: `AnonymousSweepTests.cs:20-33`, `VendorScopingTests.cs:77-114`, `SearchScopingTests.cs:81-82`, `DemoGatingTests.cs` |
| **OD-n** | Operator ruling n (§6) — cited where the ruling, not code evidence, settled the cell |

Legend for role cells: **✓** allow · **–** deny. Cells settled by a ruling carry
the OD reference in the Evidence column. Endpoint routes omit the `/api` prefix.

## 3. The [AllowAnonymous] set — UNCHANGED from Slice F

Verbatim from `AnonymousSweepTests.cs:20-33`; the sweep test must pass unchanged
(README-FIRST constraint 2). These 11 endpoints get **no action assignment**; the
drift-proof test treats this exact list as the only exemption set.

```
GET  api/health
GET  api/auth/dev-users            (404 outside demo mode — preserved, AuthController.cs:30)
POST api/auth/dev-login            (404 outside demo mode — preserved, AuthController.cs:57)
POST api/onboarding/resolve
GET  api/onboarding/draft
PUT  api/onboarding/draft
POST api/onboarding/draft/submit
POST api/onboarding/draft/documents
DELETE api/onboarding/draft/documents/{key}
POST api/onboarding/draft/resubmit
POST api/onboarding/draft/raise-clarification
```

## 4. The matrix — endpoint × action × role (AS RULED)

142 authenticated endpoints (102 at ruling + the permissions read + D3's
seven /api/views endpoints + D4's aggregate/series + eight dashboards/metrics
endpoints + D5's seven custom-field endpoints + D6's six /api/segments and
two /api/segment-assignments endpoints + D7's five /api/entry-forms
endpoints, the resolve read and two /api/numbering endpoints − the retired
legacy dashboard
read), grouped into actions A1–A71 (one
action per row, 70 live with A1 retired; the RoleMatrix suite generates one
test case per action). Every endpoint appears exactly once. Coverage tally
in §4.6. D4 note: /views/{id}/aggregate and /views/{id}/series ride A59
(they are view reads with the same dynamic record-type check). D6 note:
their `groupBy` parameter adds no endpoint and no action — a grouped
aggregate is still a view read over the same scoped pipeline. D7.5 note:
likewise /views/{id}/run's `page`/`size` params — paging an existing A59
read moves nothing in this matrix (the slice sits above the scoped source;
ViewRunPaginationTests pins scoping across pages).

### 4.1 All-principal actions

| # | Action | Endpoints | B | Ap | TE | CE | Ad | V | Evidence |
|---|---|---|---|---|---|---|---|---|---|
| ~~A1~~ | ~~ViewDashboard~~ | ~~GET /dashboard~~ | | | | | | | **RETIRED at D4 Phase 4 (sanctioned):** the legacy endpoint + DashboardService are deleted; A62 UseDashboards is the successor. The per-principal derivations it owned (Approver creator≠me SoD, evaluators' assigned-to-me, vendor scoping) migrated to SystemMetricService with their semantics test-pinned BEFORE deletion (the ruled condition) |
| A2 | ViewPersonas | GET /auth/personas | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | REG (Slice F): authenticated-only; powers the persona switcher (`identity.tsx:23`) |
| A58 | ViewPermissions | GET /auth/permissions | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | Phase 3 (this slice's file plan, as ruled): every principal reads its OWN allowed-action list, from which the web derives display gating. Anonymous → 401 (not on the §3 exemption list) |
| A59 | UseSavedViews | GET /views · GET /views/fields · GET /views/{id}/run | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | D3 (ruled): the run ADDITIONALLY checks the view's record-type View\* action via `ViewVocabulary.ViewActionFor` — e.g. TechEvaluator × Vendor view → 403 (test-pinned) |
| A60 | ManageOwnSavedViews | POST /views · PUT /views/{id} · DELETE /views/{id} | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | D3 (ruled): own views only (owner checks in service; foreign private view → 404 existence-hiding); vendor scoping of the executor's sources makes vendor-owned views safe |
| A61 | ManageSharedViews | POST /views/{id}/share | ✓ | – | – | – | ✓ | – | D3 (ruled): sharing is publication — Buyer, Admin; its own endpoint so the no-orphan drift sweep holds |
| A62 | UseDashboards | GET /dashboards/mine · GET /metrics · GET /metrics/{id}/value · GET /metrics/{id}/series | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | D4 (ruled): metric reads ADDITIONALLY check the metric's RequiredAction (SystemMetricService catalog) — e.g. evaluator × spend metric → 403, vendor metrics vendor-only (test-pinned) |
| A63 | ManageOwnDashboard | POST /dashboards/personalize · PUT /dashboards/mine · DELETE /dashboards/mine | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | D4 (ruled): copy-on-write personalization; reset falls back to the role-default union |
| A64 | ManageRoleDashboards | PUT /dashboards/role-defaults/{role} | – | – | – | – | ✓ | – | D4 (ruled): role defaults are platform configuration (OD-3 posture), Admin only |
| A65 | ManageCustomFields | GET/POST /custom-fields · PUT /custom-fields/{id} · POST /custom-fields/{id}/active · DELETE /custom-fields/{id} | – | – | – | – | ✓ | – | D5 (ruled): defs are platform configuration; zero-value hard-delete only, valued defs deactivate-only forever |
| A68 | ManageSegments | GET/POST /segments · PUT /segments/{id} · POST /segments/{id}/values · POST /segments/{id}/applications · DELETE /segments/{id}/applications/{type} | – | – | – | – | ✓ | – | D6: segment defs/values/applications are platform configuration (A65 posture). System defs (IsSystem) reject all writes — the convergence BACKLOG row owns changes to the four PR dimensions. Unapply with live assignments → 409: dimension keys are never silently dropped |
| A69 | ManageEntryForms | GET/POST /entry-forms · PUT /entry-forms/{id} · DELETE /entry-forms/{id} · PUT /entry-forms/{id}/roles | – | – | – | – | ✓ | – | D7 (OD-D7-7): entry-form definitions are platform configuration (A65/A68 posture). IsSystem (Standard) forms reject writes/deletes — the seeded layout is the parity baseline. RecordType restricted to Requisition this slice (OD-D7-5) |
| A70 | ManageNumbering | GET /numbering · PUT /numbering/{recordType} | – | – | – | – | ✓ | – | D7 (OD-D7-6): schemes shape codes at FORMAT time over the untouched Slice G gap-free generator; reads ride the manage row (A65 precedent). All four format-change uniqueness cases test-pinned (NumberingTests) |
| A66 | ReadCustomValues | GET /custom-values/{type}/{id} · GET /segment-assignments/{type}/{id} | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | D5 (ruled; the "no new rows" prior withdrawn): + dynamic record-type View\* + the record's EXISTING scoped detail fetch — vendors read only records they can reach (test-pinned). **D6 (ruled fold): segment-assignment reads are the same species — same three layers, same role set. If the role sets ever diverge, THAT is the moment the rows split** |
| A67 | EditCustomValues | PUT /custom-values/{type}/{id} · PUT /segment-assignments/{type}/{id} | ✓ | – | – | – | – | – | D5 (ruled): Buyer only, deny-by-default (Admin per OD-3; vendors pending a concrete need); + dynamic View\* + scoped fetch. **D6 (ruled fold): segment-assignment writes ride the same row — split only if the role sets ever diverge.** System-segment assignments on PRs additionally 409 ("edit the PR"): dimension columns stay the single truth |
| A71 | ReadEntryForms | GET /entry-forms/resolve | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | D7 (OD-D7-7): every principal resolves ITS OWN form (fixed global role precedence → Standard fallback) + dynamic record-type View\* inside — the 4TH use of the convention (vendor × Requisition → 403, test-pinned). The caller can never name a form: server-side re-resolution at submit is the anti-bypass (OD-D7-2) |
| A3 | Search | GET /search | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | OD-4 (MODIFIED): all roles keep search, but **result-type filtering by the caller's permitted View\* actions lands in THIS slice (Phase 2)** — search must not undo OD-2's vendor-master denial through the side door. Scoping test: TechEvaluator searching a vendor name → zero vendor-type hits. Vendor in-query scoping unchanged (TEST `SearchScopingTests.cs:81-82`) |
| A4 | DownloadFile | GET /files/{id} | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | SVC: `FileAccessPolicy.cs:15-30` fail-closed vendor scoping; TEST `VendorScopingTests.cs:77-84` pins internal-reads-any + vendor deny-on-uncertainty. Evaluators need bid attachments to score |
| A5 | UploadFile | POST /files | ✓ | – | – | – | – | ✓ | OD-5: Buyer + Vendor only — narrowest surface matching real affordances. SVC: `FilesController.cs:23` ownership stamping unchanged |
| A6 | ViewCustomLists | GET /custom-lists · GET /custom-lists/{code} | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | SCREEN: field options resolve via custom lists everywhere (`ui/useFieldOptions.ts`, `lib/lookups.ts:15`); vendor BidForm renders customList-driven fields (`BidForm.test.tsx:68`) |

### 4.2 Internal read actions

| # | Action | Endpoints | B | Ap | TE | CE | Ad | V | Evidence |
|---|---|---|---|---|---|---|---|---|---|
| A7 | ViewRequisitions | GET /requisitions · GET /requisitions/{id} | ✓ | ✓ | – | – | ✓ | – | PRs never reach vendors (`SearchService.cs:35-40`); Ap per OD-1; TE/CE denied per OD-2 |
| A8 | ViewRfqs | GET /rfqs · GET /rfqs/{id} | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | SVC: list vendor-scoped (`RfqService.cs:33-34`, TEST `VendorScopingTests.cs:94-101`); detail carries T1 mark-viewed for vendors (REG row 8). TE/CE keep RFQ context per OD-2. Obs-1 (§7): vendor detail resource-scoping is a Slice-F patch after RM |
| A9 | ViewBidOpenings | GET /rfqs/{id}/opening | ✓ | ✓ | ✓ | ✓ | ✓ | – | Evaluators' working surface (`EvalScreens.tsx`); Ap per OD-1. Closes Obs-2 (read was unguarded, `EvaluationService.cs:18-22`) |
| A10 | ViewTechnicalEval | GET /rfqs/{id}/technical-eval | ✓ | ✓ | ✓ | ✓ | ✓ | – | SVC: alias masking for evaluator-only principals preserved untouched (`EvaluationService.cs:166-194`, BUSINESS-RULES [G]); Ap per OD-1. Closes Obs-2 (`EvaluationService.cs:62-66`). Eval reads stay role-gated; per-RFQ assignment remains a write-side service check |
| A11 | ViewAwards | GET /awards · GET /rfqs/{id}/award · GET /rfqs/{id}/award-eligibility | ✓ | ✓ | – | – | ✓ | – | Approver ✓ settled (judging requires the award context, `DashboardService.cs:71-72`); TE/CE denied per OD-2. SVC: sealed-until-reveal + bidder aliases unchanged (`AwardService.cs:35-41`). Closes Obs-3 (all three reads were unguarded) |
| A12 | ViewPos | GET /pos · GET /pos/{id} | ✓ | ✓ | – | – | ✓ | ✓ | SVC: vendor scoping `PoService.cs:23,38`; Ap per OD-1; TE/CE per OD-2 |
| A13 | ViewAsns | GET /asns · GET /asns/{id} · GET /asns/{id}/grn · GET /pos/{poId}/ship-plan | ✓ | ✓ | – | – | ✓ | ✓ | SVC: vendor scoping `DeliveryService.cs:23-24,36,48`; Ap per OD-1; TE/CE per OD-2. Obs-4 (§7): GRN-for-ASN foreign-vendor resource scoping rides the same Slice-F patch as Obs-1 |
| A14 | ViewInvoices | GET /invoices · GET /invoices/{id} · GET /pos/{poId}/billable | ✓ | ✓ | – | – | ✓ | ✓ | SVC: vendor scoping `InvoiceService.cs:23,36,44`; Ap per OD-1 |
| A15 | ViewStatements | GET /statements · GET /statements/{vendorId} | ✓ | ✓ | – | – | ✓ | – | Closes Obs-5 (list was unguarded — any vendor could read all balances, `StatementService.cs:19-27`). Vendors keep A57. Ap per OD-1 |
| A16 | ViewVendors | GET /vendors · GET /vendors/{id} | ✓ | ✓ | – | – | ✓ | ✓ | Vendor ✓ test-pinned (`VendorScopingTests.cs:104-114`, bank masked last-4 per `VendorService.cs:20-26`). **TE/CE explicitly DENIED (OD-2 ruling): preserves bidder alias masking end-to-end** — an evaluator must not be able to de-alias bidders via the vendor master. Ap per OD-1 |
| A17 | ViewAuditTrail | GET /pos/{id}/audit · GET /vendors/{id}/audit | ✓ | ✓ | – | – | ✓ | – | OD-10: internal-only — audit trails contain internal actor names. SPSB-OPEN-QUESTIONS §A row added (vendor transparency variant, actor names redacted). Ap per OD-1 |
| A18 | ViewForms | GET /forms · GET /forms/{id} | ✓ | ✓ | – | – | ✓ | – | Buyer tooling; questionnaires are embedded in RFQ/token DTOs, not fetched from /forms. Ap per OD-1 |
| A19 | ViewSwecTaxonomy | GET /swec | ✓ | ✓ | – | – | ✓ | – | SCREEN: SwecPicker in vendor-master screens + OnboardingForm. Obs-6 (§7): the anonymous onboarding form's dependency on this authenticated endpoint is a pre-staging BACKLOG item. Ap per OD-1 |
| A20 | ViewOnboarding | GET /onboarding/templates · GET /onboarding/invitations · GET /onboarding/applications · GET /onboarding/applications/{id} | ✓ | ✓ | – | – | ✓ | – | Buyer screens (NAV Setup); Ap per OD-1 |
| A21 | ViewClarifications | GET /clarifications · GET /clarifications/thread | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | SVC: vendor-scoped (`ClarificationService.cs:17,45-46`); TE/CE per OD-2 (scoring questions arise) |
| A22 | ViewUsers | GET /users | ✓ | ✓ | ✓ | ✓ | ✓ | – | OD-6: all internal roles — `RfqBuilder.tsx:63` (Buyer assigns evaluators) and `EvalScreens.tsx:53` (committee names) depend on it; DTO is name/email/roles, no secrets |
| A23 | ViewVendorLogins | GET /users/vendor-logins | – | – | – | – | ✓ | – | SCREEN: only `AdminUsers.tsx`; GATE `gating.tsx:52` |

### 4.3 Internal write actions — sourcing

| # | Action | Endpoints | B | Ap | TE | CE | Ad | V | Evidence |
|---|---|---|---|---|---|---|---|---|---|
| A24 | ManageRequisitions | POST /requisitions · PUT /requisitions/{id} · POST /requisitions/{id}/submit · POST /requisitions/{id}/cancel · POST /requisitions/{id}/lines/{lineId}/{cancel\|release\|reopen\|reserve\|unreserve} (9 endpoints) | ✓ | – | – | – | – | – | GATE `createPr` → Buyer; Admin excluded per OD-3 (no transactional writes) |
| A25 | ManageRfqDraft | POST /rfqs · PUT /rfqs/{id} | ✓ | – | – | – | – | – | GATE `createRfq` → Buyer; `RfqService.cs:105` OwnerUserId |
| A26 | ManageRfqLifecycle | POST /rfqs/{id}/release · POST /rfqs/{id}/close · POST /rfqs/{id}/cancel | ✓ | – | – | – | – | – | Buyer governance actions (REG posture: Buyer/RFQ owner) |
| A27 | InviteVendorToRfq | POST /rfqs/{id}/invitations | ✓ | – | – | – | – | – | **REG row 1**; covers ReInviteVendor (REG row 4, T8 same endpoint); GATE `gating.tsx:38,41` |
| A28 | RescindRfqInvitation | POST /rfqs/{id}/invitations/{vendorId}/rescind | ✓ | – | – | – | – | – | **REG row 2**; GATE `gating.tsx:39` |
| A29 | ExtendRfq | POST /rfqs/{id}/extend | ✓ | – | – | – | – | – | **REG row 3** (G5 forward-only/capped stays in service); GATE `gating.tsx:40` |
| A30 | OpenTechnicalEnvelope | POST /rfqs/{id}/open-technical | – | – | ✓ | – | – | – | SVC: `EvaluationService.cs:26-29` — role gate coarse, per-RFQ assignment check stays service-level |
| A31 | OpenCommercialEnvelope | POST /rfqs/{id}/open-commercial | ✓ | – | – | ✓ | – | – | SVC: `EvaluationService.cs:44-53` — dual: assigned CommEvaluator; single: Buyer opens. Envelope rules stay in service |
| A32 | ScoreTechnical | POST /rfqs/{id}/scores | – | – | ✓ | – | – | – | SVC: `EvaluationService.cs:70-77` assignment check. Obs-7 (§7): body-EvaluatorId hardening is a BACKLOG row, not RM |
| A33 | FinalizeTechnical | POST /rfqs/{id}/finalize-technical | ✓ | – | ✓ | – | – | – | OD-8: Buyer + TechEvaluator — matches the screen evidence (`EvalScreens.tsx:5`) while the process owner can close out; all-vendors-scored guard stays in service |
| A34 | SubmitAward | POST /rfqs/{id}/award | ✓ | – | – | – | – | – | SVC: `AwardService.cs:72`; SoD creator≠approver enforced downstream |
| A35 | ApproveAward | POST /awards/{id}/approve | – | ✓ | – | – | – | – | **Settled by existing enforcement:** `AwardService.cs:145-148` — Approver role at threshold RM 0 + creator≠approver SoD (BUSINESS-RULES [G]). The catalog row mirrors the service |

### 4.4 Internal write actions — P2P, vendor master, setup, admin

| # | Action | Endpoints | B | Ap | TE | CE | Ad | V | Evidence |
|---|---|---|---|---|---|---|---|---|---|
| A36 | IssuePo | POST /pos/{id}/issue | ✓ | – | – | – | – | – | SVC: `PoService.cs:44`; buyer action post-award; Admin excluded per OD-3 |
| A37 | ReceiveGoods | POST /asns/{id}/receive | ✓ | – | – | – | – | – | SVC: `DeliveryService.cs:103`; [Q] GRN caps stay in domain |
| A38 | ApproveInvoice | POST /invoices/{id}/approve | ✓ | – | – | – | – | – | OD-7: Buyer. **The PV/DoA control point supersedes this at L5** (payment approval is the real DoA gate; out of scope here) |
| A39 | ResolveInvoiceException | POST /invoices/{id}/resolve | ✓ | – | – | – | – | – | OD-7: Buyer; [G] exception-blocks-payment stays in domain; same L5 note as A38 |
| A40 | ManageVendors | POST /vendors · POST /vendors/manual · PUT /vendors/{id} · PUT /vendors/{id}/categories | ✓ | – | – | – | – | – | GATE `createVendor` → Buyer |
| A41 | ToggleVendorStatus | POST /vendors/{id}/toggle-status | ✓ | – | – | – | ✓ | – | OD-3 sub-ruling: Buyer + Admin — **master-data governance, not a transaction**. Note: blacklist/suspend workflows (roadmap Phase 2) will formalise this further |
| A42 | InviteOnboarding | POST /onboarding/invitations · POST /onboarding/invitations/{id}/resend | ✓ | – | – | – | – | – | GATE `inviteOnboarding` → Buyer |
| A43 | RevokeOnboardingInvitation | POST /onboarding/invitations/{id}/revoke | ✓ | – | – | – | – | – | **REG row 9** (Slice K); GATE `gating.tsx:46` |
| A44 | ReviewOnboardingApplication | POST /onboarding/applications/{id}/{start-review\|clarify\|approve\|reject} (4 endpoints) | ✓ | – | – | – | – | – | Buyer review flow; vendor side rides the anonymous token endpoints (§3) |
| A45 | ManageForms | POST /forms · PUT /forms/{id} · DELETE /forms/{id} | ✓ | – | – | – | ✓ | – | GATE `createForm` → Buyer; **Admin added per OD-3** (forms named in the administrative-writes list) |
| A46 | ManageCustomLists | POST /custom-lists · POST /custom-lists/{code}/values · PUT /custom-lists/values/{valueId} · DELETE /custom-lists/values/{valueId} | – | – | – | – | ✓ | – | GATE `createCustomList` → Admin. Crawl note: the functional e2e "Admin Custom Lists" test ran this write as u_faridah — a crawl bug (test name says Admin); fixed to u_admin in Phase 1, per the OD-9 principle |
| A47 | ManageUsers | POST /users · PUT /users/{id} | – | – | – | – | ✓ | – | GATE `createUser` → Admin; Vendor-role-on-internal blocked (`UserService.cs:34-35,53`) |
| A48 | SendClarification | POST /clarifications | ✓ | – | ✓ | ✓ | – | ✓ | **REG row 10** + GATE `gating.tsx:45` (Buyer + vendor); TE/CE per OD-2 as recommended (scoring questions arise). Approver – (OD-1: single write is ApproveAward) and Admin – (OD-3: not in the administrative-writes list) — both deny-by-default per the general rule; one catalog line each to flip if ruled otherwise |

### 4.5 Vendor-principal actions

| # | Action | Endpoints | B | Ap | TE | CE | Ad | V | Evidence |
|---|---|---|---|---|---|---|---|---|---|
| A49 | ViewMyInvitations | GET /my/rfqs | – | – | – | – | – | ✓ | SVC: `BidService.cs:20-24` |
| A50 | DeclineRfqInvitation | POST /my/rfqs/{rfqId}/decline | – | – | – | – | – | ✓ | **REG row 5**; SVC `RfqVendorService.cs:67-76` |
| A51 | DeclareIntendToBid | POST /my/rfqs/{rfqId}/intend | – | – | – | – | – | ✓ | **REG row 6** (T2/T4); SVC `RfqVendorService.cs:67-76` |
| A52 | WithdrawBid | POST /my/rfqs/{rfqId}/withdraw-bid | – | – | – | – | – | ✓ | **REG row 7** (T6); SVC `RfqVendorService.cs:51-65` |
| A53 | SubmitBid | GET /rfqs/{rfqId}/my-bid · PUT /rfqs/{rfqId}/my-bid · POST /rfqs/{rfqId}/my-bid/submit | – | – | – | – | – | ✓ | SVC: `BidService.cs:44,101-112` + deadline [G] `BidService.cs:86-91` |
| A54 | AcknowledgePo | POST /pos/{id}/acknowledge | – | – | – | – | – | ✓ | **OD-9: Vendor ONLY — closes a real probity hole** (any internal user could acknowledge for any vendor via `PoService.cs:58` EnsureCanAccess). Crawl verified: no e2e step performs this as an internal persona |
| A55 | CreateAsn | POST /pos/{poId}/asns | – | – | – | – | – | ✓ | OD-9: Vendor only; [Q] no-over-ship clamps stay in domain (`DeliveryService.cs:62-80`). Crawl verified clean |
| A56 | SubmitInvoice | POST /pos/{poId}/invoices | – | – | – | – | – | ✓ | OD-9: Vendor only — internal users can no longer fabricate vendor-side documents; 3-way match [Q] stays in domain (`InvoiceService.cs:58`). Crawl verified clean |
| A57 | ViewMyStatement | GET /my/statement | – | – | – | – | – | ✓ | SVC: `StatementService.cs:39` |

**MarkInvitationViewed** (REG row 8) remains the passive T1 side effect of
`GET /rfqs/{id}` and rides A8 (`RfqService.cs:86-92`).

### 4.6 Coverage tally (drift-proof cross-check)

| Bucket | Endpoints |
|---|---|
| [AllowAnonymous] (§3) | 11 |
| A1–A6 + A58 all-principal | 8 |
| A59–A61 saved views (D3, + D4's aggregate/series) | 9 |
| A62–A64 dashboards + metrics (D4) | 8 |
| A65–A67 custom fields (D5) + D6 segment-assignments riding A66/A67 | 9 |
| A68 segments (D6) | 6 |
| A69–A70 entry forms + numbering manage (D7) | 7 |
| A71 entry-form resolve (D7) | 1 |
| A7–A23 internal reads | 35 |
| A24–A48 internal writes | 49 |
| A49–A57 vendor actions | 11 |
| **Total** | **153 live** (Slice RM's 113 + permissions + D3 views + D4 dashboards/metrics + D5 custom fields + D6 segments/segment-assignments + D7 entry-forms/numbering − the retired A1) — matches the ApiExplorer surface (same enumeration as `AnonymousSweepTests.cs:44-51`). (A1 ViewDashboard retired with the legacy GET /api/dashboard at D4 Phase 4 — sanctioned.) |

## 5. The Vendor principal's complete action list (as ruled)

**Shared, resource/masking-scoped:** ViewDashboard, ViewPersonas, ViewPermissions,
Search (in-query scoping + Phase 2 type filter), DownloadFile (own files),
UploadFile (stamped Bid-owned), ViewCustomLists, ViewRfqs (invitation-scoped list +
T1 mark-viewed), ViewPos, ViewAsns, ViewInvoices, ViewVendors (bank masked —
test-pinned), ViewClarifications, SendClarification.

**Vendor-exclusive:** ViewMyInvitations, DeclineRfqInvitation, DeclareIntendToBid,
WithdrawBid, SubmitBid, AcknowledgePo, CreateAsn, SubmitInvoice, ViewMyStatement.

**Everything else → 403**, including (all reachable before RM, none used by the
vendor UI): statements list, bid openings, technical eval, all award reads,
requisitions, audit trails, onboarding buyer surface, forms, swec, users.

## 6. OPERATOR RULINGS — recorded 2026-07-11

| OD | Ruling | Rationale (operator's, verbatim where given) |
|---|---|---|
| OD-1 | **APPROVED (a):** Approver = full internal read tier; single write ApproveAward | "An approver cannot discharge DoA responsibility without reading the PR→RFQ→bid→award context" |
| OD-2 | **APPROVED (a):** evaluators evaluation-surface-only — scoring endpoints + evaluation reads + RFQ context + clarifications; **vendor master explicitly DENIED** | "Preserves bidder alias masking end-to-end" |
| OD-3 | **APPROVED (a):** Admin = all-internal reads + administrative writes (users, custom lists, forms, numbering when it exists); no transactional writes. Sub-ruling: ToggleVendorStatus = Buyer + Admin (master-data governance, not a transaction) | Blacklist/suspend workflows (roadmap Phase 2) will formalise vendor-status governance further |
| OD-4 | **MODIFIED:** all internal roles keep /api/search, but result-type filtering by the caller's permitted READ actions lands **in this slice (Phase 2)**, not the backlog. New scoping test: TechEvaluator searching a vendor name → zero vendor-type hits | "Search returning vendor-name hits to an evaluator undoes OD-2 through the side door." Catalog-driven and cheap once the catalog exists |
| OD-5 | **APPROVED:** file upload = Buyer + Vendor only | — |
| OD-6 | **APPROVED:** GET /users readable by all internal roles; user writes Admin-only | Builder and scoring screens depend on it (evidence on row A22) |
| OD-7 | **APPROVED:** invoice approve/resolve = Buyer | The PV/DoA control point supersedes this at L5 |
| OD-8 | **APPROVED:** finalize-technical = Buyer + TechEvaluator | — |
| OD-9 | **APPROVED:** PO acknowledge / ASN create / invoice submit = Vendor principal ONLY | "Closes a real probity hole (internal users could fabricate vendor-side documents today)." Crawl verified: no e2e step performs these as an internal persona (the adjacent custom-lists persona bug is fixed in Phase 1 per the same principle) |
| OD-10 | **APPROVED:** PO audit trail internal-only (contains internal actor names) | SPSB-OPEN-QUESTIONS §A row added: vendor transparency variant (actor names redacted) for SPSB to decide |

**General rule (operator):** anything the matrix left implicit → **deny**.
Loosening later is a catalog line; tightening later is a fight. Applied to: A48
Approver/Admin denial (noted on the row).

**Ruling-numbering reconciliation** (recorded for the audit trail): the operator's
scope-veto message labelled the RFQ-detail item "Obs-4" and the onboarding-/swec
item "Obs-7"; in this document those are **Obs-1** and **Obs-6** respectively. Both
vetoes are sustained as described by content, not number. The document's own Obs-4
(GRN read) is the same resource-scoping class as Obs-1 and rides the same
post-RM Slice-F micro-commit; the document's Obs-7 (SetScore body-EvaluatorId),
whose in-slice fix was also offered for veto, stays out of RM per the same
sustained veto and gets its own BACKLOG hardening row.

## 7. Observations — dispositions as ruled

- **Obs-1 — RFQ-detail vendor scoping (`RfqService.cs:42-83`): NOT in RM.**
  BACKLOG row (operator wording): fix as a standalone Slice-F-patch micro-commit
  immediately after RM lands.
- **Obs-2 — opening/technical-eval reads unguarded: CLOSED by A9/A10** (vendor and
  non-permitted roles → 403).
- **Obs-3 — award reads unguarded: CLOSED by A11.**
- **Obs-4 — GRN-for-ASN read unguarded (`DeliveryService.cs:95-99`): the in-slice
  one-liner is VETOED**; foreign-vendor GRN resource scoping joins the Obs-1
  micro-commit (same class, same patch). The role gate still removes all
  non-permitted roles.
- **Obs-5 — statements list unguarded: CLOSED by A15.**
- **Obs-6 — anonymous onboarding form depends on authenticated /swec (+
  custom-list lookups), masked today by the demo header: NOT in RM** (would change
  the frozen AllowAnonymous set). BACKLOG row: design a token-scoped or anonymous
  read-only lookups endpoint for the onboarding surface — **before staging deploy**.
- **Obs-7 — SetScore validates body EvaluatorId, not the caller
  (`EvaluationService.cs:70-77`): NOT in RM** (veto sustained, §6 reconciliation).
  BACKLOG hardening row.

## 8. File plan — Phases 1–3 (as ruled; deviations from the Step 0 draft noted)

### Phase 1 — Catalog + enforcement (one commit)
| File | Change |
|---|---|
| `api/src/eProcure.Application/Authorization/ApiActions.cs` | NEW — one string constant per action. **Deviation from draft: catalog lives in Application, not Api** — OD-4's Phase 2 search filter needs `SearchService` (Infrastructure) to consult it; Application is referenced by both. Pure code, no framework deps — clean-architecture safe |
| `api/src/eProcure.Application/Authorization/ActionCatalog.cs` | NEW — THE declarative map `action → allowed roles`, one row per matrix line, each commented `// A-nn` |
| `api/src/eProcure.Api/Auth/ActionAttribute.cs` | NEW — `sealed class ActionAttribute : AuthorizeAttribute` setting `Policy = "action:{name}"` |
| `api/src/eProcure.Api/Auth/ActionPolicyProvider.cs` | NEW — resolves `action:*` policies from the catalog; unknown action → fail-closed deny; falls back to the default provider otherwise (401 path byte-identical) |
| `api/src/eProcure.Api/Auth/ForbiddenProblemHandler.cs` | NEW — `IAuthorizationMiddlewareResultHandler`: authenticated-but-denied → 403 problem-details "Not permitted for your role"; 401 fallback and §3 404-masking untouched |
| `api/src/eProcure.Api/Program.cs` | DI registrations |
| 19 controller files | `[Action(ApiActions.X)]` per endpoint — attribute-only diff |
| `api/tests/.../Integration/ActionAssignmentSweepTests.cs` | NEW — drift-proof: every endpoint outside §3 carries exactly one catalog-known action; new unassigned endpoint = red CI (constraint 6) |
| `e2e-audit/tests/03-functional.spec.ts` | crawl bug: the "Admin Custom Lists" write test runs as u_admin (was u_faridah) — OD-9 principle |

### Phase 2 — Authorization test matrix + the OD-4 search filter (one commit)
| File | Change |
|---|---|
| `api/tests/.../Integration/RoleMatrixTests.cs` | NEW — generated from the catalog (`[MemberData]` over ActionCatalog × Roles.All): allowed → non-401/403; denied → exactly 403; placeholder-GUID route params |
| test factory / principals helper | Testing-environment principal per single role (incl. pure Approver); shipped DevUserStore/personas untouched |
| `api/src/eProcure.Infrastructure/Services/SearchService.cs` | OD-4: internal results filtered per record type by the caller's permitted View\* action (Vendor→ViewVendors, PR→ViewRequisitions, RFQ→ViewRfqs, PO→ViewPos, Invoice→ViewInvoices); vendor path unchanged |
| `api/tests/.../Integration/RoleSearchFilterTests.cs` | NEW — TechEvaluator searching a vendor name → zero vendor-type hits (+ per-type coverage); existing `SearchScopingTests` UNCHANGED |
| existing suites | AnonymousSweepTests, VendorScopingTests, SearchScopingTests, DemoGatingTests — unchanged, green |

### Phase 3 — Web derivation (one commit)
| File | Change |
|---|---|
| `AuthController.cs` | NEW `GET /api/auth/permissions` → caller's allowed actions (catalog × claims); anonymous → 401 (NOT added to §3) |
| `web/src/api/client.ts` (+ regenerated `schema.ts`) | `getPermissions` |
| `web/src/identity.tsx` | fetch permissions once post-identity, cached |
| `web/src/ui/gating.tsx` | static RULES map **DELETED**; `<Gated>`/`useGate` consume the server list |
| new web test | pins that gating renders from the server list |
| `docs/PERMISSIONS-REGISTER.md` | rewritten to point here as the living document; "ONE remaining authorization item" closed |
| crawl | full run at close (and before Phase 1) — display behaviour per persona unchanged |

## 9. Five-lens review (as ruled)

- **Vendor/end-user:** the vendor's 22-action surface is pinned; nothing a vendor
  legitimately does changes. Probes the portal never makes (all balances, eval
  data, award pricing) now 403. OD-9 closes the reverse direction too: internal
  users can no longer author vendor-side documents.
- **Buyer/procurement user:** Buyer holds every action their screens exercise;
  zero flow change. OD-7 keeps invoice control where the prototype puts it, with
  the L5 PV/DoA supersession on the record.
- **Solution architect:** one declarative catalog in Application (single seam for
  policies AND the OD-4 search filter); web derives gating at runtime; drift-proof
  sweep extends the Slice F ApiExplorer technique; no schema change; service-layer
  checks remain as the fine-grained layer — defense in depth.
- **Senior programmer:** attribute-only controller diffs; constants not strings;
  catalog rows comment their matrix line; Phase 2 tests generated from the catalog
  so matrix and tests cannot diverge; deny-by-default for everything implicit.
- **CTO:** the register's last authorization item closes; D3 unblocks. Marginal
  cost of the next module: one catalog row + one attribute; a forgotten assignment
  is red CI. This document, as ruled, is the handover artifact for who-may-do-what.
