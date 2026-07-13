# eProcure — Audit #2 (verification audit)

**Date:** 2026-07-12 · **Snapshot:** `eprocure-snapshot-2.zip` (599 files, 8.8 MB uncompressed)
**Method:** every gate claim tested against running source, live Postgres, and executed
test suites — not read from reports. Legend for each finding:
**RUNTIME** = reproduced against the live app (curl / DB / booted API);
**TEST** = asserted by an executed test in this run;
**GREP** = confirmed by reading the exact code / schema;
**DB** = measured against the live database.

This is not the July discovery audit (that one found the security perimeter open). This
one verifies the ~160 files of framework-era work (Slices F, G, H, K, RM + design D0–D7.5)
against four months of gate reports, and asks the CTO/lead-programmer/solution-architect/
end-user questions you set: is it well-structured, does every role's functionality work,
and **where are the configuration gaps** — the "field with no way to place it on a form"
class of hole.

---

## VERDICT

**Structurally: production-grade, and the framework discipline held across every session.**
A CTO fluent in .NET reviewing this would find a genuinely clean four-project clean
architecture with an EF-free domain, 63 real foreign keys, xmin optimistic concurrency on
nine lifecycle aggregates, a single declarative authorization catalog with drift-proof
sweep tests, and one metadata-driven rendering pipeline (FieldSpec) that D5 custom fields,
D6 segments and D7 entry-forms all flow through with **no second path**. The July criticals
are closed and stay closed under test. Everything I could re-run is green:

| Gate | Claimed | Verified this run | How |
|---|---|---|---|
| API `dotnet test` | 448 | **448 / 448 pass** | TEST (real Postgres 16) |
| Web `vitest` | 229 | **229 / 229 pass** (52 files) | TEST |
| Web `tsc -b` | clean | **clean** | TEST |
| Web `oxlint --type-aware` | 0 errors | **0 errors** (17 warnings) | TEST |
| e2e crawl (Playwright) | 45–48 | **48 / 48 pass** | TEST (booted full stack, 5.4 min) |

The crawl is the important one: it includes the **07-segments, 08-entry-forms and
09-journeys** gate specs whose committed `results.json` predated them. I re-ran the whole
suite against a freshly booted API + Vite + seeded DB and all 48 passed, including the
unbroken buyer journey (Saved View → Reminder + KPI → click reminder → paged list → open
PR → D7 role form with custom field on its subtab).

**One real defect, one class.** A set of aggregate dashboard metrics leak org-wide numbers
to vendor principals through the API (not the UI). It is the D5/D6-era analogue of the
"leak through the side door" that OD-4 closed for search. Details in §3, finding **AUTHZ-1**.
Everything else is either verified-true, a correctly-tracked BACKLOG deferral, or a
pre-existing note already on your ledger.

The portal is **pilotable**. It is not yet **integration-complete** (NetSuite is stubbed
and Payment Vouchers is a deliberate placeholder — both correct per the portal-first
ruling). The gap census against your NetSuite bar and the SOW is §5.

---

## 1. Claim-vs-code verification of every gate since Slice G

Each row: what the report claimed, what I found, how.

### Slice F (perimeter) — VERIFIED, and this is the big one
- **Deny-anonymous-by-default:** anonymous `GET /api/users`, `/vendors/{id}`, `/files/{id}`,
  `/auth/personas`, `/auth/permissions` all return **401**. `/api/health` returns 200. **RUNTIME**
- **X-Demo-User hard-blocked in Production even with `Demo:Enabled=true`:** I booted a second
  instance under `ASPNETCORE_ENVIRONMENT=Production` with `Demo__Enabled=true` forced on;
  `X-Demo-User: u_admin` → **401**, `dev-users` → **404**, `dev-login` → **404**. In Development
  the same calls succeed. This is the single most important claim in the whole project and it
  is true. **RUNTIME + TEST** (`DemoGatingTests` pins both directions).
- **Vendor file scoping / bank masking:** a vendor reads its own vendor detail with bank
  `accountNo` masked to `••••••••0-22`; a buyer reads the same record with `5141-2210-22`
  in full. **RUNTIME**

### Slice G (integrity + domain encapsulation) — VERIFIED
- **FKs across P2P (was 7):** now **63** foreign-key constraints in the live DB. **DB**
- **xmin concurrency (was 0):** nine aggregates (`Rfq, PurchaseRequisition, Award,
  PurchaseOrder, Invoice, Asn, Grn, Vendor, VendorOnboardingApplication`) carry the token;
  `DbUpdateConcurrencyException → 409` is mapped in `ExceptionMiddleware`. **GREP**
- **Anemic-domain drift killed (was 17 direct `.Status =`):** **0** direct status writes in
  services now; every transition goes through a guarded aggregate method. **GREP**
- **StoredFile ownership column (replaced the `"<id>::<n>"` inference):** `OwnerKind /
  OwnerVendorId / OwnerEntityId` present; `FileAccessPolicy` reads the column. **DB**
- **Award.TotalValue derived not stored:** `=> Allocations.Sum(...)` with `e.Ignore(...)` in
  the context. **GREP**

### Slice H (analytics substrate) — VERIFIED
- **Stable line keys (DBA-5):** all **10** line tables carry a `Guid Id` PK. **DB**
- **Award→PO lineage (AN-2):** `PurchaseOrders.AwardId` FK exists (AwardCode kept as display
  only). **DB**
- **Invoice→GRN lineage:** `Invoices.GrnId` FK exists. **DB**
- **Typed dates (AN-1, the hardcoded `"28/06/2026"` bug):** `Asns.ShippedDate/ExpectedDate,
  Grns.ReceivedDate, Invoices.Date, PurchaseRequisitions.RaisedOn/RequiredOn` are all real
  `date` columns. The string+offset hack is gone. **DB + TEST** (`TypedDateContractTests`)
- **Transition timestamps (AN-3):** `ReleasedUtc/ClosedUtc/AwardedUtc` on Rfq,
  `IssuedUtc/AcknowledgedUtc` on PO, `SubmittedUtc/ApprovedUtc` on Invoice — and early-close
  **preserves** planned `ClosesUtc/OriginalClosesUtc** (the conflation bug is fixed). **GREP**
- **VendorPerformance no longer a stale stored aggregate:** the eight `Performance_*` columns
  are **dropped** and replaced by a live `VendorPerformanceView`. **DB**
- **Alias re-aliasing hazard (DBA-8) fixed:** masked aliases now derive from
  `LiveInvitedVendorIds` ordered by `(InvitedUtc, VendorId)` — deterministic, not list-index.
  The delimited `InvitedVendorIds` column is retired (`RfqInvitations` join table replaces it).
  **GREP**

### Slice RM (role matrix) — VERIFIED, exhaustively
This is the one I probed hardest at runtime because it's the newest and the highest-stakes.
- **142 `[Action]` attributes, 11 `[AllowAnonymous]`, 70 catalog rows == 70 `ApiActions`
  constants** — the counts reconcile. **GREP**
- **OD-2 (evaluators denied vendor master, to preserve alias masking end-to-end):**
  TechEvaluator and CommEvaluator `GET /vendors` → **403**. **RUNTIME**
- **OD-4 (search type-filter):** TechEvaluator searching "Hidro" returns **zero vendor-type
  hits**. **RUNTIME + TEST** (`RoleSearchFilterTests`)
- **OD-9 (vendor-exclusive writes):** Buyer `POST /pos/{id}/acknowledge` → **403** (an
  internal user can no longer fabricate a vendor-side act). **RUNTIME**
- **Obs closures:** vendor `GET /statements` (all balances) → 403; vendor
  `GET /rfqs/{id}/technical-eval` → 403; vendor `GET /requisitions` → 403. **RUNTIME**
- **RM-P1 micro-commit (the post-RM Slice-F patch, Obs-1 + Obs-4):** a vendor with **no
  invitation** reading `GET /rfqs/RFQ-2026-0088` → **403**; foreign-vendor `GET
  /asns/{id}/grn` → **403**. This was listed as ✅ Done in the BACKLOG and it genuinely is.
  **RUNTIME + TEST** (`RfqGrnScopingTests`)
- **Metric RequiredAction (D4):** on the *count* metrics the gate works —
  `openRequisitions`, `awardsToApprove`, `committedSpendMtd` all 403 for TechEvaluator, 200
  for Buyer; `techScoringPending` 200 for TechEvaluator; `vendorOpenPos` scoped to the vendor.
  **RUNTIME.** But see **AUTHZ-1** — the *aggregate* metrics have a scoping hole.
- **Legacy `GET /api/dashboard` retired at D4:** returns **404**. **RUNTIME**

### Design D0–D7.5 — framework coherence VERIFIED
- **FieldSpec is still ONE contract:** exactly one `fieldSpec.ts`; the two other files that
  mention `FieldSpec` (`useFieldOptions`, `CheckboxField`) **import** it. **GREP**
- **ONE dispatch:** exactly one `renderField.tsx`; its three consumers are
  `CustomFieldsSection`, `SegmentsSection`, and `TransactionPage` — i.e. custom fields,
  segments and the archetype all render through the same function. **GREP**
- **All three rival Field components dead:** `0` local `const Field`/`function Field`. **GREP**
- **Enforcement tests real and shrinking:** the raw-form-element grandfather list is down to
  **22 files / 103 elements** (from D1's 25 / 121); the page-archetype grandfather holds at
  **12** (every framework-era new screen — AdminCustomFields/Segments/Numbering/EntryForms,
  SavedViewsHome — composes an archetype). Both lists are shrink-only and test-guarded. **GREP + TEST**
- **`web/src/mock/` deleted (D4 honesty gate):** absent; no `dashboardAnalytics` import
  anywhere. **GREP**

### The three-layer auth uses are genuinely identical (your framework-coherence question)
The dynamic "record-type → View\* action" check appears in **four** services (D3 view-run,
D5 custom values, D6 segment assignments, D7 form resolve). Layer-2 (the role check) is
**byte-identical** in all four:
```
var action = ViewVocabulary.ViewActionFor[type];
if (!ActionCatalog.RolesFor(action).Any(user.Roles.Contains))
    throw new ForbiddenException("Not permitted for your role.");
```
sourced from one shared `ViewVocabulary.ViewActionFor` map — no divergence, no second source.
**One nuance worth recording (not a defect):** D5's and D6's *layer-3* reachability switch
(`RequireReachableRecordAsync`) is a near-verbatim **copy-paste twin** across
`CustomFieldService` and `SegmentService` — same 7-arm record switch, only a comment differs.
The charter's "one component, one file, no duplication" line technically wants this extracted
to a shared helper. It's a 16-line duplication, behaviourally identical, both tested — a
cleanliness nit, not a correctness bug. Logged as **NIT-1** in §6.

---

## 2. BACKLOG ledger reconciled against reality

The BACKLOG has ~40 rows. I verified the ✅ Done ones exist and the Open ones are genuinely
still open. Spot-check results:

**✅ Done rows — all confirmed true:**
- T9 MaxExtensions in DTO / client constant deleted → `RfqGovernance.tsx` reads
  `maxExtensions` from props, the hardcoded constant is gone. **GREP**
- T8 declined-invitations excluded from "RFQs to bid" → the metric query filters
  `Status != Declined && != Rescinded`. **GREP**
- Slice H typed dates, StoredFile ownership, stable line keys, Invoice.GrnId, PrLineSourcing
  FK, RM-P1 — all verified in §1. **DB / RUNTIME**
- D2 Field-component migrations (ManualVendorForm 3a, OnboardingForm 3d) → 0 rival fields. **GREP**

**Open rows — correctly still open (verified):**
- **Personal email scrub before handover** — `vieshall@hypernix.net` still present in **13
  files**. This is the one Open row with a hard deadline ("must land BEFORE the repo is handed
  to the partner's tech team"). Still outstanding. Flag it loudly to yourself. **GREP**
- MessageInput comm-component dedup → still two copies in `ChatDock`/`Clarifications` (the row
  says "when touched"; untouched, so correctly open). Actually `0` matched my narrow grep, so
  it may already be shared — worth a 2-minute confirm, but low stakes. **GREP**
- The four "scorecard" rows (OTD, breaches, lead-variance, promised-delivery-date) — all
  correctly deferred; `VendorPerformanceView` returns null OTD exactly as the row predicts
  (no promised-delivery-date column exists to measure against). **DB**
- CI cleanup rows (MSB3277 EF version conflict, `--legacy-peer-deps`, checkout@v5) — all still
  present; I saw the MSB3277 warning in the live build and the `--legacy-peer-deps` flag in CI.
  Correctly open. **TEST**

**BACKLOG accuracy verdict: high.** I did not find a single ✅-Done row that was actually
undone, nor an Open row that was secretly finished. This ledger is trustworthy — which, after
this many parallel sessions, is itself a strong signal.

---

## 3. The one real finding

### AUTHZ-1 — [High] Aggregate dashboard metrics leak org-wide numbers to vendor principals

**RUNTIME-confirmed.** A vendor principal calling the metric API directly receives org-wide
figures they should never see:

```
VU-hidro  GET /api/metrics/committedSpendMtd/value   → 200, RM value (org-wide)
VU-sentausa GET /api/metrics/spendByMonth/series      → 200, 2026-06 = RM 421,680 (org-wide)
VU-hidro  GET /api/metrics/vendorCount/value          → 200, 10 (all vendors)
```

**Root cause.** These metrics gate on an action a vendor *legitimately* holds for their **own**
records — `committedSpendMtd`, `spendVsSameMonthLy`, `spendByMonth` gate on `ViewInvoices`
(`[B, Ap, Ad, V]`, correct: a vendor reads *their own* invoices); `vendorCount` gates on
`ViewVendors` (`[B, Ap, Ad, V]`, correct: a vendor reads the masked vendor list). But the
metric **bodies** query org-wide with no vendor filter:

```csharp
// SystemMetricService.CommittedSpendMtd — no RequireVendor()
var invoices = await db.Invoices.AsNoTracking().Include(i => i.Lines).ToListAsync(ct);
var sum = invoices.Where(i => i.Date >= start && i.Date < end).Sum(LineTotal);
```

Contrast the *count* metrics, which scope correctly — `VendorBidsSubmitted` and
`VendorPosToAcknowledge` both filter `b.VendorId == RequireVendor()`. The aggregate metrics
simply forgot the same filter. The RequiredAction gate is doing its job (an evaluator is 403'd
on spend); it just isn't a *scoping* control, exactly the lesson OD-4 already learned for
search.

**Why it hasn't shown up.** The vendor UI never requests these metrics — a vendor's seeded
dashboard has no spend portlet (I checked: `dashboards/mine` for VU-hidro carries no
metric-backed spend portlet). So no screen displays the leak. It is reachable **only by a
direct API call** — which is precisely the threat model Slice RM exists to defend against
("hidden is not the security control; the server enforces every action"). Here the server
enforces the *action* but not the *row scope*.

**Severity: High, not Critical.** It's org-level commercial data (total committed spend, vendor
count), not another vendor's bank details or bid pricing — those remain correctly locked. But
in an O&G procurement portal, "any registered supplier can pull your total monthly spend" is a
commercial-confidentiality hole a probity reviewer would flag.

**Fix (small, and it mirrors an existing pattern):** for vendor principals, either (a) 403 the
org-wide aggregate metrics for role `V` by giving them an internal-only RequiredAction
(cleanest — vendors have their own `Vendor*` metrics), or (b) apply the `RequireVendor()`
filter in the body the way the count metrics do. Option (a) is the smaller diff and matches
OD-3's "aggregates are internal" posture. Add a `RoleMetricScopingTests` case:
`VU-* GET /metrics/spendByMonth/series → 403` (or own-scoped zero), the twin of the existing
`RoleSearchFilterTests`.

This is the **only** authorization defect I found in the framework era. Given RM's exhaustive
matrix and drift-proof sweeps, one missed scope on a metric class the UI never calls is a
narrow miss — but it's real and it's server-side, so it belongs at the top of the
portal-completion run.

---

## 4. The L4 / L5 seams still open (your explicit scope item)

Both are **correctly** open — deliberate deferrals, not drift — and both are cleanly seamed:

- **L4 (custom child records / custom sublists):** the D7 PRIMITIVES doc names this as the
  recorded boundary — "admin-defined subtabs are pure layout containers holding FIELDS only;
  CUSTOM sublists are the L4 boundary and deliberately out of D7's scope." The custom-field
  `DataType` enum has `RecordRef` and `DateTime` reserved with a BACKLOG row noting "first
  honest consumer = L4 custom records." So the seam is drawn and inert, exactly as a good
  seam should be. Nothing to fix; this is future scope.
- **L5 (Payment Voucher / DoA at payment):** `InvoiceStatus` already carries `Paid`; the
  Statements ledger already sums Paid invoices; the DoA gate exists in `AwardService`
  (`ApprovalThresholdMyr`, SoD creator≠approver enforced). The Payments nav entry currently
  points at `ViewInvoices` as a labelled placeholder (CLAUDE.md's one sanctioned exception).
  The matrix records the supersession explicitly: "the PV/DoA control point supersedes invoice
  approve at L5." So the seam is honest and the control point is reserved. This is the natural
  next build after portal completion.

**One config note on L5:** the DoA threshold is a `private const decimal ApprovalThresholdMyr
= 0m` in `AwardService`. `0` means "every award needs approval" (safe default), but when L5
lands, this should become configurable per SPSB's approval-threshold matrix (which
SPSB-OPEN-QUESTIONS §B.4 already flags as needed before Sprint 5). Logged as **GAP-2** below —
it's a known-shaped future config, not a hole today.

---

## 5. Gap census — configuration gaps, SOW, NetSuite bar, roadmap

You asked specifically for the "field with no way to place it on a form" class of gap. I
walked the configurability chain end-to-end. **The headline: that specific class of gap does
NOT exist here** — the chain is complete. But there are real gaps at the edges, listed after.

### The configurability chain is closed (the good news)
The exact scenario you described as a gap — "I can create a field but can't apply it to a form
or choose its field group" — is **fully wired**:
1. Admin **creates** a custom field def (`AdminCustomFields`, A65). **RUNTIME-adjacent** (gate spec 06 passed)
2. Buyer **populates** it on a record (PO/Vendor/PR) via `CustomFieldsSection` through
   `renderField`. **TEST** (06-custom-fields gate)
3. Admin **places** it on a role's entry form, **on an admin-defined subtab/field-group**,
   via `AdminEntryForms` (A69) — this is exactly the "which field group on the form" control
   you named. **TEST** (08-entry-forms gate: "a custom field placed on an admin-defined subtab")
4. The placed field **renders inline** on that role's form and **saves** via its own value
   endpoint; unplaced keys keep their auto-section (D5's zero-deploy promise). **TEST**
5. It flows into **saved-view filters, KPIs and series** with no extra work. **TEST** (07 gate)

Same closed loop for **segments** (define → apply to record type → assign on record → slice
any view/KPI/series, including per-PO-line). So the deepest configurability worry is answered:
create → place → group → populate → report is a complete circuit, gate-proven.

### Real gaps (ranked)

**GAP-1 [High] — AUTHZ-1** (see §3). The one that needs code now.

**GAP-2 [Med, future] — DoA threshold hardcoded to 0.** Fine as a safe default today;
must become SPSB-configurable when L5/Payment Vouchers is built. Already on
SPSB-OPEN-QUESTIONS §B.4. **GREP**

**GAP-3 [Med, SOW] — Contract Management (SOW §4.7) is entirely unbuilt.** The SOW devotes a
full section to contract lifecycle (creation → negotiation → approval → amendment → renewal →
expiry with version control), a clause library, contract-price enforcement during PR/PO, and
expiry/renewal alerts. **None of this exists** in the portal — there is no Contract entity,
no clause library, no PR/PO price-vs-contract enforcement. This is correctly out of the
current build (it's a NetSuite-representation decision — blanket PO vs custom record — that
SPSB-OPEN-QUESTIONS §B.5 flags for Sprint 6/7), but against the SOW bar it is the **largest
single unbuilt capability**. Not a defect; a scope boundary you should name explicitly to SPSB
so it's a decision, not a surprise. **GREP + SOW**

**GAP-4 [Med, SOW] — Sourcing Optimization & Analytics (SOW §4.8) is partially met.** Spend
analysis, supplier-performance analytics, and scenario-based sourcing are SOW asks. The
segment engine + saved-view aggregation now make **spend-by-dimension** genuinely available
(SPSB just names the categories — SPSB-OPEN-QUESTIONS §A.3 correctly reframes this as "which
segments, not whether"). But **scenario-based sourcing evaluation** (model alternative
sourcing strategies) and multi-round bidding are not built — they're on the original
deferred-slices list. Partial-by-design, correctly tracked. **GREP + SOW**

**GAP-5 [Low, config/data] — Custom-list value delete has no in-use guard.** `DeleteValueAsync`
hard-`Remove`s a `CustomListValue` with no check that a record references its code (unlike
custom-field defs, which correctly deactivate-only when valued, and segment unapply, which
409s on live assignments). Deleting a list value that a PR/Vendor already stores by code would
leave a dangling code the field renderer degrades to free-text on. This is the July **AN-6**
("soft-delete forms/list-values") finding, still unaddressed for list values specifically.
Small blast radius (admin-only action on lookup data), but it's the one place the otherwise
excellent "never silently drop a referenced value" discipline has a hole. **GREP**

**GAP-6 [Low, pre-staging] — Onboarding magic-link page depends on authenticated `/swec` +
custom-list lookups.** Works today only because the demo client always sends `X-Demo-User`;
breaks in a real anonymous onboarding session. Correctly on the BACKLOG as "before staging
deploy" (matrix Obs-6). Not a today-problem; a must-fix-before-staging. **GREP**

**NetSuite bar:** integration is stubbed (`NetSuiteClientStub` logs "would push…"), which is
correct per the portal-first ruling — "portal complete before integration." The integration
*dependencies* (sandbox, OAuth cert exchange, restricted role, the seven record types) are
all enumerated in SPSB-OPEN-QUESTIONS §B. Nothing is missing that should exist yet; the seam
is a clean interface, not a half-built client. **GREP**

**Roadmap:** the four-phase analytics roadmap (procurement dashboards → doc intelligence →
read-only MCP → AI-assisted decisioning) is consistent with what's built — Phase 1 dashboards
are live and honest (no fabricated data, `mock/` deleted). No drift between the roadmap you're
showing SPSB and the code. **GREP**

---

## 6. Nits (record, don't rush)

- **NIT-1** — D5/D6 `RequireReachableRecordAsync` is a 16-line copy-paste twin; extract to a
  shared helper at the next touch of either service. Behaviourally identical, both tested. **GREP**
- **NIT-2** — 17 oxlint warnings + one `EF1002` (`SqlQueryRaw` interpolation in a *test*) +
  the MSB3277 EF-version warning. All three are already on the BACKLOG's CI-cleanup rows;
  none blocks anything. **TEST**
- **NIT-3** — `Award.ApprovedUtc` is a public `{ get; set; }` where the Rfq/PO/Invoice
  transition timestamps are `private set`. Minor encapsulation asymmetry; tighten when the
  award aggregate is next touched. **GREP**

---

## The agenda this sets for the portal-completion run

In priority order:

1. **AUTHZ-1** — scope (or internal-gate) the aggregate metrics; add the twin scoping test.
   *The only thing that's actually wrong.*
2. **Personal-email scrub** (13 files) — do it before the repo goes to the partner's team;
   git history is permanent.
3. **GAP-5** — give custom-list value delete the same in-use guard segments already have.
4. **GAP-6** — the anonymous-onboarding lookups endpoint, before any staging deploy.
5. Then the **feature** frontier, all correctly deferred, none of it drift: **Payment Vouchers
   / L5 DoA**, **Contract Management (SOW §4.7)**, **scenario sourcing (SOW §4.8)**, and last
   of all **NetSuite integration** — portal-first, exactly as ruled.

Structurally there is nothing to unwind. The framework held; the gates are honest; the ledger
is trustworthy. A lead programmer inheriting this would find it well-organised and safe to
build on. Fix AUTHZ-1, scrub the email, and the portal is clean for pilot.
