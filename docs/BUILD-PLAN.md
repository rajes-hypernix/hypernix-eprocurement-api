# BUILD-PLAN.md

Build eProcure as ordered **vertical slices**. One slice per session. After each slice,
run it on localhost, click through the verify checklist, then start the next.

---

## ⚠️ TESTING MANDATE — applies to EVERY slice (no exceptions)

The goal is **all of the prototype's current functions running as a REAL .NET + React
application on the user's localhost.** Not a mock. Not "would work with a backend." Real.

**STANDING INSTRUCTION — append this to every prompt and obey it:**
> Test against `prototype/eprocure-portal.html` continuously. After building, verify the
> .NET + React behaviour MATCHES the prototype for this slice — open the prototype,
> read its `<script>` for exact rules, and/or run `cd prototype/tests && npm test`. Keep
> iterating until `dotnet test` and `npm run test` are green AND the localhost behaviour
> equals the prototype's. `dotnet test` passing is not enough — the outcome must match
> the prototype. Then give me the exact localhost run commands and a verify checklist.
> No excuses, no mocks, no "coming soon" for in-scope features — it must work for real on
> `http://localhost:5173`. If you can't verify something, say so and keep working.

## Scope for now (integration OFF)

- **Vendors** → seed **dummy** vendors from `docs/SEED-DATA.md` (no NetSuite).
- **Requisitions (PRs)** → seed **dummy** requisitions from `docs/SEED-DATA.md` (no NetSuite).
- **Payment Vouchers** → **leave blank** — nav entry + empty placeholder only, no logic.
- **NetSuite** → **stub only** (no-op/log interface). No real integration yet.
- Everything else (RFQ, bidding, evaluation, award, PO, ASN, GRN, Invoice, Statements)
  is built for real and must match the prototype.

---

## Slice 0 — Foundations & walking skeleton

**Goal:** an app that builds, connects to Postgres, serves Swagger, shows the React
shell with the prototype's nav, and has the cross-cutting services + dev seeder.

**Create:** AppDbContext (Npgsql); `IClock`, `ICurrentUser`, `ICodeGenerator`, `IAuditLog`
+ `AuditEntry`; a no-op `INetSuiteClient` stub; `GET /api/health`; dev JWT login for the
seeded users; a Development **DataSeeder** hook (used from Slice 1 on); first migration;
React shell (top bar + sidebar nav matching the prototype) + generated API client calling
health.

**Done when:** docker compose + migration + `dotnet run` + `npm run dev` all work; Swagger
shows health; the React shell renders the nav and shows API health OK on localhost.

> **PROMPT:** "Read CLAUDE.md and all docs/ (including PROTOTYPE.md and SEED-DATA.md).
> Implement **Slice 0** from docs/BUILD-PLAN.md. Restate the goal, files, and rules first.
> Build the walking skeleton: AppDbContext (Npgsql), cross-cutting services (IClock,
> ICurrentUser, ICodeGenerator, IAuditLog + AuditEntry), a no-op INetSuiteClient stub, a
> health endpoint, dev JWT login for the seeded users, a Development DataSeeder hook, the
> first EF migration, and the React shell with the prototype's nav + a generated API
> client that calls health. Add tests for the code generator and audit writer.
> THEN OBEY THE STANDING INSTRUCTION: test against prototype/eprocure-portal.html, run
> prototype/tests if useful, keep iterating until dotnet test + npm test are green AND the
> shell/nav matches the prototype, then give me the localhost commands + verify checklist.
> No mocks, no excuses. Do not start Slice 1."

---

## Slice 1 — Vendor Master + Users & roles (DUMMY vendors)

**Goal:** supplier master + internal user/role management, seeded with **dummy** data.

**Create:** Vendor, VendorUser, User entities + migration; CRUD endpoints; role
assignment; SWEC categories; the DataSeeder that inserts the dummy vendors + users from
SEED-DATA.md (no NetSuite); React Vendor Master (tabs) + Admin user screens.

**Rules:** [G] internal users ≠ vendors; [A] audit on vendor/user create/edit/status; [G]
access-scoping groundwork. Vendors are dummy/local — do NOT call NetSuite.

**Done when:** list/create/edit dummy vendors + users on localhost; status changes are
audited; SoD separation tested; screens match the prototype's Vendor Master/Admin.

> **PROMPT:** "Implement **Slice 1** (Vendor Master + Users & roles) per docs/BUILD-PLAN.md,
> docs/SEED-DATA.md, and the [G]/[A] rules in docs/BUSINESS-RULES.md. Vendors and users are
> DUMMY/local seed data — no NetSuite. Restate first; build entities, migration, endpoints,
> the dummy-data seeder, and the React Vendor Master + Admin screens; audit every change.
> THEN OBEY THE STANDING INSTRUCTION: test against prototype/eprocure-portal.html, keep
> iterating until dotnet test + npm test are green AND the screens/behaviour match the
> prototype, then give localhost commands + verify checklist. No mocks, no excuses. Stop."

---

## Slice 2 — Requisitions → RFQ draft → release (DUMMY requisitions)

**Goal:** PR worklist (dummy), select lines, create RFQ, build the question form (all 12
field types + sections), save draft, release.

**Create:** PR/PrLine seeded as **dummy** data (SEED-DATA.md, no NetSuite);
Rfq/RfqLine/RfqForm/FormItem + FormLibrary; endpoints for PR list, RFQ create/draft/
release, form CRUD; React Requisitions + RFQ Builder (settings, single/dual, question
editor, evaluator assignment).

**Rules:** [A] audit on RFQ release; [$] codes (RFQ-2026-xxxx); capture `ClosesUtc` at
release. PRs are dummy/local — no NetSuite.

**Done when:** create an RFQ from dummy PRs, add every question type, set single/dual +
closing date + evaluators, save draft, resume, release (audited) — matching the prototype.

> **PROMPT:** "Implement **Slice 2** (Requisitions → RFQ draft → release) per the docs.
> Requisitions are DUMMY seed data (SEED-DATA.md) — no NetSuite. Cover all 12 FormItem
> types and Technical/Commercial grouping; audit release; capture ClosesUtc.
> THEN OBEY THE STANDING INSTRUCTION: test against prototype/eprocure-portal.html (the
> Requisitions + RFQ builder screens), keep iterating until tests are green AND behaviour
> matches the prototype, then localhost commands + verify checklist. No excuses. Stop."

---

## Slice 3 — Vendor bidding (+ deadline enforcement)

**Goal:** vendor opens an invitation, answers every field type, prices lines (partial/
alt), attaches files, validates, saves draft, submits.

**Create:** Bid/BidLine/BidAnswer; vendor-portal bid endpoints; React bid form.

**Rules:** **[G] bid deadline** — reject create/update/submit unless `Status==Open &&
now<=ClosesUtc`; [G] access scoping (vendor sees only its own); [A] audit on submission.

**Done when:** a vendor submits on an open RFQ; submit after close returns 409; vendor B
can't read vendor A's bid; submission audited — matching the prototype (see fixes.js).

> **PROMPT:** "Implement **Slice 3** (vendor bidding) per the docs. Enforce the bid
> deadline and vendor access scoping server-side; audit submissions. Include tests that
> submit-after-close is 409 and vendor B can't read vendor A's bid.
> THEN OBEY THE STANDING INSTRUCTION: test against prototype/eprocure-portal.html and run
> prototype/tests (esp. fixes.js for the deadline rule); iterate until tests green AND
> behaviour matches the prototype; localhost commands + verify checklist. No excuses. Stop."

---

## Slice 4 — Bid opening + technical evaluation (+ masking)

**Goal:** close bids (server), open envelopes, score technical, finalize pass/fail.

**Create:** TechnicalScore; close/open endpoints; scoring endpoints + weighted/committee
calc + `TechPass`; React Bid Openings + scoring matrix.

**Rules:** [G] sealed bids (commercial not readable pre-open); **[G] masking** bound to
principal + phase (evaluators get Bidder A/B/C, never real names, even via role switch);
[A] audit open/score/finalize.

**Done when:** evaluators score + finalize; commercial not returned before the gate;
evaluator API never reveals identity; matches the prototype (see mask behaviour).

> **PROMPT:** "Implement **Slice 4** (bid opening + technical evaluation) per the docs.
> Enforce sealed-bid gating and server-side masking (identities never returned to
> evaluator-role calls, not defeatable by role switch); audit open/score/finalize. Include
> a masking-cannot-be-defeated test.
> THEN OBEY THE STANDING INSTRUCTION: test against prototype/eprocure-portal.html; iterate
> until tests green AND behaviour matches the prototype; localhost commands + verify
> checklist. No excuses. Stop."

---

## Slice 5 — Commercial + Award (eligibility, qty cap, approval/DoA)

**Goal:** reveal commercial after finalize, compare/rank, allocate (split), submit for
approval, approve, generate POs.

**Create:** Award/AwardAllocation + Approval; commercial reveal + comparison endpoints;
allocate/confirm/approve endpoints; PO generation on approval; React award screen.

**Rules:** [G] commercial reveal only after finalize; **[G] eligibility** (dual+finalized
→ only TechPass vendors); **[Q] qty ≤ min(required, offered)**; **[G] award approval/DoA**
(different approver, threshold) before POs; [A] audit the award. (NetSuite push = stub.)

**Done when:** allocate among passed vendors only; can't over-allocate; can't award a
failed/non-bidding vendor; needs a separate approver; approval generates one PO per vendor
— matching the prototype (see fixes.js for eligibility + qty cap).

> **PROMPT:** "Implement **Slice 5** (commercial + award) per the docs. Enforce eligibility
> (TechPass only), qty cap (≤ min(required, offered)), and award approval/DoA (different
> authorised user) before generating POs (NetSuite push stub). Audit the award. Tests for
> each rejected case.
> THEN OBEY THE STANDING INSTRUCTION: test against prototype/eprocure-portal.html and run
> prototype/tests (fixes.js); iterate until tests green AND behaviour matches the
> prototype; localhost commands + verify checklist. No excuses. Stop."

---

## Slice 6 — Purchase Orders (issue, acknowledge)

**Goal:** PO list/detail, issue draft POs, vendor acknowledges; lifecycle states.

**Create:** PO/PoLine; issue/ack endpoints; NetSuite push via the **stub** interface;
React PO screens (buyer + vendor).

**Rules:** [A] audit issue/ack; [$] PO codes; [G] vendor sees only its POs. NetSuite stub.

**Done when:** buyer issues, vendor acknowledges, states/audit update, vendor scoping
holds — matching the prototype's PO screens.

> **PROMPT:** "Implement **Slice 6** (Purchase Orders) per the docs: issue + acknowledge
> with audit and vendor scoping; NetSuite push via the stub interface (no real integration).
> THEN OBEY THE STANDING INSTRUCTION: test against prototype/eprocure-portal.html and run
> prototype/tests (e2e.js covers PO); iterate until tests green AND behaviour matches the
> prototype; localhost commands + verify checklist. No excuses. Stop."

---

## Slice 7 — ASN + GRN (quantity guards)

**Goal:** vendor raises ASN; buyer receives (GRN) with over/under-receipt handling.

**Create:** Asn/AsnLine, Grn/GrnLine; remaining-to-ship engine; endpoints; React
Deliveries (both portals).

**Rules:** **[Q] ASN no over-ship/duplicate**; **[Q] GRN cap ≤ shipped & ≤ outstanding;
under-receipt = Short and frees the shortfall**; [A] audit.

**Done when:** can't over-ship, can't duplicate-ship a committed line, can't over-receive,
short receipts free the shortfall — matching the prototype (see safety.js).

> **PROMPT:** "Implement **Slice 7** (ASN + GRN) per the docs with the full remaining-to-
> ship engine and over/under-receipt guards. Tests for over-ship, duplicate ASN, over-
> receipt, and short-receipt-frees-shortfall.
> THEN OBEY THE STANDING INSTRUCTION: test against prototype/eprocure-portal.html and run
> prototype/tests (safety.js); iterate until tests green AND behaviour matches the
> prototype; localhost commands + verify checklist. No excuses. Stop."

---

## Slice 8 — Invoice + 3-way match

**Goal:** vendor submits invoice; buyer sees line-level match; approve / exception /
resolve; SST/WHT.

**Create:** Invoice/InvoiceLine; match engine vs PO+GRN; endpoints; React AP screens.

**Rules:** **[Q] invoice qty ≤ received − already-invoiced**; **[Q] price variance >
tolerance → Exception**; **[G] exception blocks payment**; [$] SST 8%/WHT server-side;
[A] audit.

**Done when:** over-billing rejected; variance → Exception; exception invoices not payable;
totals recomputed server-side — matching the prototype (see safety.js + e2e.js).

> **PROMPT:** "Implement **Slice 8** (Invoice + 3-way match) per the docs. Enforce the
> billable cap, price-tolerance exception, and exception-blocks-payment; compute SST/WHT
> server-side. Tests for each.
> THEN OBEY THE STANDING INSTRUCTION: test against prototype/eprocure-portal.html and run
> prototype/tests (safety.js, e2e.js); iterate until tests green AND behaviour matches the
> prototype; localhost commands + verify checklist. No excuses. Stop."

---

## Slice 9 — Payment Voucher  ⏸️ DEFERRED (leave blank for now)

**Do not build PV logic yet.** Per current scope, Payment Vouchers normally come via
NetSuite and are out of scope for now. Build only a **blank placeholder**: the nav entry
and an empty "No payment vouchers — coming soon" screen. No entities, no endpoints, no
logic. Revisit when explicitly asked.

> **PROMPT (only if asked to stub the screen):** "Add a blank Payment Vouchers placeholder
> screen (nav entry + empty state 'No payment vouchers yet'). No backend, no logic — it is
> deferred. Confirm it renders on localhost and matches the prototype's nav placement. Stop."

---

## Slice 10 — Statements / SOA

**Goal:** per-vendor running ledger, aging, GRNI accrual, export; vendor reconciliation
upload.

**Create:** derivation services (no new mutable tables); endpoints; React Statements (both
portals).

**Rules:** [G] vendor sees only its statement; [$] amounts recomputed server-side.

**Done when:** ledger/aging/GRNI render and reconcile; export works — matching the
prototype (see soa behaviour / e2e.js).

> **PROMPT:** "Implement **Slice 10** (Statements/SOA) per the docs as server-side
> derivations with vendor scoping.
> THEN OBEY THE STANDING INSTRUCTION: test against prototype/eprocure-portal.html; iterate
> until tests green AND behaviour matches the prototype; localhost commands + verify
> checklist. No excuses. Stop."

---

## Slice 11 — NetSuite integration  ⏸️ DEFERRED (stub only for now)

**Do not implement real NetSuite integration yet.** Keep the `INetSuiteClient` as a
no-op/log stub everywhere. When explicitly asked later, this slice replaces the stub with
a queued, retryable client (PO, Vendor Bill, Item Receipt, Bill Payment), surfaces
failures, and adds deploy configs (Railway + Azure Malaysia West).

---

## After the in-scope slices (0–8, 10)

- End-to-end test: full dual-envelope and single-envelope cycle to a paid... (Payment is
  deferred) — run to a matched/approved invoice + reconciled statement, mirroring the
  prototype's proven flows (e2e.js).
- Then, when asked: Payment Vouchers (Slice 9), NetSuite integration (Slice 11), and the
  out-of-original-scope SOW modules (Intelligent Replenishment 4.4, Contract Management
  4.7, Landed Cost 4.6, self-service PR creation + budget 4.3, multi-round + scenario
  optimisation 4.2) — each its own slice, same pattern, same testing mandate.
