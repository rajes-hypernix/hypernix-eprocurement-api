# BUILD PROMPT — Slice A2F: Audit #2 Fixes

You are Claude Code working on eProcure (.NET 10 / EF Core 10 / React+TS / PostgreSQL).
Read `docs/A2F/README-FIRST-A2F.md` first — it holds the session contract, task order,
and hard boundaries. Read `docs/reviews/full-audit-2026-07-12.md` for the findings this
slice closes. Then read `CLAUDE.md`, `docs/AUTHORIZATION-MATRIX.md`, and
`docs/design-framework/CHARTER.md` so you build to the existing conventions.

This slice closes every audit finding **except the deferred feature frontier**. It is a
hardening slice. Every task is small and precisely located. Do not scope-creep.

**START WITH STEP 0.** List every file you will create or modify, per task, one line each,
then STOP and wait for my confirmation before writing code.

---

## T1 — [SECURITY] AUTHZ-1: stop aggregate metrics leaking org-wide numbers to vendors

**The defect (verified at runtime in the audit):** a vendor principal can call
`GET /api/metrics/committedSpendMtd/value`, `.../spendVsSameMonthLy/value`,
`.../spendByMonth/series`, and `.../vendorCount/value` and receive **org-wide** figures
(e.g. RM 421,680 total committed spend). These metrics gate on `ViewInvoices` / `ViewVendors`,
which role `V` legitimately holds for its OWN records — but the metric bodies in
`SystemMetricService` query org-wide with no `RequireVendor()` filter. The count metrics
(`VendorBidsSubmitted`, `VendorPosToAcknowledge`, `VendorOpenPos`) already scope correctly;
these four forgot.

**Fix — option (a), the internal-gate approach (matches OD-3 "aggregates are internal"):**
Give the four org-wide aggregate/roster metrics a RequiredAction that role `V` does NOT hold,
so a vendor principal is cleanly **403'd** rather than served a scoped-zero. Concretely:

- Introduce (or reuse, if a suitable one exists) an internal-only action for org aggregates.
  The cleanest is a dedicated `ViewSpendAnalytics` action mapped to `[B, Ap, Ad]` (no `V`),
  used by the spend metrics; and gate `vendorCount` on an internal-only action too (it is a
  management roster count, not the masked vendor *list* a vendor may read). Do NOT reuse
  `ViewInvoices`/`ViewVendors` for these four — that's the whole bug.
- Update the `MetricDescriptor` rows for `CommittedSpendMtd`, `SpendVsSameMonthLy`,
  `SpendByMonth`, and `VendorCount` to the new internal action(s).
- Leave every genuinely vendor-scoped metric exactly as it is (`VendorRfqsToBid`,
  `VendorBidsSubmitted`, `VendorPosToAcknowledge`, `VendorOpenPos` — these gate on
  `ViewMyInvitations` and filter by `RequireVendor()`; do not touch them).
- Add the new action(s) to `ApiActions`, `ActionCatalog` (with an `// A-row` comment), and
  the AUTHORIZATION-MATRIX doc, following the exact format of the surrounding rows. The
  `ApiActions`-count == `ActionCatalog`-row-count invariant must still hold.

**Tests (add — this is a new scoping test, twin of `RoleSearchFilterTests`):**
Create `api/tests/eProcure.Tests/Integration/RoleMetricScopingTests.cs` asserting, over the
demo personas:
- `VU-hidro` (and one more vendor) `GET /api/metrics/committedSpendMtd/value` → **403**
- `VU-hidro` `GET /api/metrics/spendByMonth/series` → **403**
- `VU-hidro` `GET /api/metrics/vendorCount/value` → **403**
- control: `u_faridah` (Buyer) → **200** on all three
- control: the vendor-scoped metrics (`vendorOpenPos`, `vendorBidsSubmitted`) still **200**
  for the vendor and return that vendor's own numbers.

**Web:** confirm no internal or vendor dashboard portlet breaks. The vendor's seeded
dashboard has no spend portlet, so nothing should change visually; if any web test or
`dashboards/mine` seed references these metric ids for a vendor, adjust the seed, not the
gate. Run the 05-dashboards crawl spec — it must still pass.

**Commit `A2F-T1:` only when** the new test passes, all 448+ API tests stay green, and the
crawl's dashboard spec passes.

---

## T2 — [SECURITY] Obs-6: token-gate the anonymous onboarding lookups

**The defect:** the vendor onboarding magic-link page needs reference/custom-list lookups
(e.g. `/swec`, custom-list values) that currently require authentication; it only works in
the demo because the client always sends `X-Demo-User`. A real anonymous applicant on the
magic link would hit 401s. (Audit GAP-6 / matrix Obs-6.)

**Fix:** expose the specific lookups the onboarding form needs through an endpoint that
authorizes by a **valid onboarding invitation token**, not by a logged-in principal. Reuse
the existing magic-link token verification (`VendorOnboardingInvitation.HashToken` /
whatever the onboarding submit path already uses to authenticate the applicant) — do NOT add
a blanket `[AllowAnonymous]` on the general reference endpoints. The pattern is: token in →
verify against `VendorOnboardingInvitations` (Sent/Opened, unexpired) → return ONLY the
lookups the onboarding form renders. Anything else stays authenticated.

**Perimeter discipline:** any new anonymous route MUST be added to BOTH sweep-test allow
lists (`AnonymousSweepTests`, `ActionAssignmentSweepTests`) with a one-line comment
explaining the token gate, exactly like the existing 11 entries. If it's not on both lists
with a comment, the sweep tests fail — that's intended.

**Tests (add):**
- valid unexpired token → 200 with the lookup payload;
- missing/garbage token → 401/403;
- expired or Completed/Revoked invitation token → 401/403;
- the endpoint returns no data beyond the onboarding form's needs (assert the shape).

**Commit `A2F-T2:`** when green and both sweep lists updated.

---

## T3 — [INTEGRITY] GAP-5: custom-list value delete needs an in-use guard

**The defect:** `CustomListService.DeleteValueAsync` hard-`Remove`s a `CustomListValue` with
no check that a record references its code — unlike custom-field defs (deactivate-only when
valued) and segment unapply (409 on live assignments). Deleting an in-use value leaves a
dangling code the field renderer silently degrades to free-text. (Audit GAP-5 / July AN-6.)

**Fix — match the existing "never silently drop a referenced value" discipline:**
- Add `IsActive` (bool, default true) to `CustomListValue`; migration name
  `CustomListValueIsActive`. This is the ONE migration this slice adds.
- Change delete semantics: if the value's code is referenced anywhere it can be stored
  (custom-field values via `ValueListCode`, and any native field sourced from the list),
  **deactivate** instead of deleting (soft-delete), returning the value in an inactive state;
  only hard-delete when unreferenced. This mirrors the custom-field-def rule exactly.
- Ensure inactive values: (a) do NOT appear in the option list offered for NEW entry, but
  (b) DO still resolve for DISPLAY of existing records that already hold the code (so history
  doesn't degrade to raw code). Follow how deactivated custom-field defs already behave.

**Tests (add):**
- delete an unreferenced value → hard-deleted, gone from options;
- delete a referenced value → deactivated, not removed; existing record still displays its
  label; the value no longer offered for new entry;
- reactivation path if the service already has one for defs (mirror it); otherwise leave
  reactivation out of scope and note it.

**Commit `A2F-T3:`** with the migration, service change, and tests in one atomic commit.

---

## T4 — [NIT-1] Extract the duplicated D5/D6 reachability guard

`CustomFieldService.RequireReachableRecordAsync` and
`SegmentService.RequireReachableRecordAsync` are a 16-line copy-paste twin (same 7-arm
record switch; only a comment differs). Extract to a single shared helper (e.g.
`RecordReachability` in Application, injected, or a shared method on a common base the two
services already share) so there is ONE source, per the charter's "one component, one file."
Behaviour must be byte-identical — the existing D3/D5/D6/D7 tests are the safety net; they
must all still pass unchanged. Do NOT change any authorization outcome. If extraction would
force a public-surface change that ripples further than these two services, STOP and report
before proceeding.

**Commit `A2F-T4:`** when all existing tests pass with zero behavioural change.

---

## T5 — [NIT-3] Tighten `Award.ApprovedUtc` encapsulation

`Award.ApprovedUtc` is a public `{ get; set; }` where the Rfq/PO/Invoice transition
timestamps are `{ get; private set; }` stamped inside guarded domain methods. Make
`ApprovedUtc` `private set` and ensure it is stamped only inside the award-approval domain
method (mirror `Invoice.MarkApproved` / `Rfq.MarkAwarded`). Adjust the one or two call sites
in `AwardService` to go through the method. No behavioural change; tests stay green.

**Commit `A2F-T5:`**.

---

## T6 — [HYGIENE] Scrub the personal handover email

`vieshall@hypernix.net` appears in ~13 files (seed data, docs, possibly tests). Replace with
a neutral placeholder consistent with the other seeded internal emails (the
`*@hypernix.test` convention already in `DevUserStore`). Do NOT change any logic — this is a
string swap. Verify: `grep -rn "vieshall@hypernix.net"` returns **0** across the repo
(including docs and tests). Note in the report that git *history* still contains it — a
history rewrite is a handover-time decision, out of scope here; flag it so it's a conscious
call, not a miss.

**Commit `A2F-T6:`** when grep is clean.

---

## T7 — [CI] Standing CI-cleanup rows

Close the BACKLOG's long-standing CI hygiene rows, each verifiable:
- **MSB3277** EF version conflict in the API build — align the EF Core / Npgsql package
  versions across projects so the warning disappears from `dotnet build`.
- **`--legacy-peer-deps`** — resolve the underlying peer-dep conflict in `web/package.json`
  so `npm install` (and CI) no longer needs the flag; update the CI workflow to drop it.
- **EF1002** — the `SqlQueryRaw` interpolation warning in a test: parameterize it.
- **`actions/checkout@v5`** — confirm/upgrade the pinned action versions in
  `.github/workflows/ci.yml`.
- **MessageInput dedup** — the audit's narrow grep suggested this may already be shared;
  confirm in 2 minutes. If `ChatDock` and `Clarifications` already import one component,
  mark the BACKLOG row done. If not, and it's genuinely two copies, extract to one shared
  `MessageInput` (this is the "when touched" row — you're touching it, so finish it).

Each is its own logical change but they may share one commit `A2F-T7:` since they're all CI
hygiene. Gates must be green; the point of the task is that the warnings are *gone*, so paste
the clean `dotnet build` / `npm install` tail in the report.

---

## T8 — [LEDGER] Truth pass + config example + tag

- **BACKLOG.md:** mark every row this slice closed as ✅ Done with the commit ref; leave the
  deferred-feature rows untouched and clearly Open.
- **AUTHORIZATION-MATRIX.md:** already updated by T1/T2 in their commits — verify it reads
  coherently as a whole (the new metric action, the token-gated onboarding route).
- **PRIMITIVES / CHARTER:** if T4's extraction changes where the reachability guard lives,
  update the one doc line that references it.
- **`appsettings.Development.json.example`:** the app needs a dev `Jwt` section + connection
  string to boot (the real `appsettings.Development.json` is correctly git-ignored). Commit a
  **`.example`** template (placeholder secrets, clearly fake) so a fresh clone can boot the
  API without reverse-engineering config. Reference it in SETUP/README.
- **Refresh `e2e-audit/results.json`** from the final full crawl run (so the committed
  results match the built code — the audit found the old ones predated three specs).
- **Tag `v0.7-audit2-fixes`** (annotated), consistent with the existing tag scheme.

**Commit `A2F-T8:`** then tag.

---

## FINAL REPORT

- Per-task: what changed, files touched, evidence (test names + counts, curl transcripts for
  T1/T2 showing the 403s, clean-build tails for T7, `grep` = 0 for T6).
- **Five-lens review** (vendor / buyer / solution architect / senior programmer / CTO) of the
  slice as a whole — especially: does T1 correctly deny vendors without denying internal
  users, and does T2 open exactly the right anonymous surface and nothing more.
- Full-crawl result (all specs, pass count) and confirmation `results.json` was regenerated
  from it.
- The BACKLOG diff (rows closed).
- Anything you hit that pushed toward the deferred frontier and you STOPPED on — I want to
  know where the boundary got tested.

Do not begin coding until I confirm your Step 0 file plan.
