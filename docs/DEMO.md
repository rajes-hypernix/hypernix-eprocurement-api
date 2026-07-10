# DEMO.md — getting to a client-ready localhost

Goal: a populated, walkable eProcure on `http://localhost:5173` that **matches the
prototype**, with dummy data (no NetSuite), Payment Vouchers blank.

## What "done for the demo" means
Every screen you'll show has data and behaves like the prototype:
Dashboard · Vendor Master · Requisitions · RFQ builder · Bid Openings · Evaluation +
Award · Purchase Orders · Deliveries (ASN/GRN) · Invoices (3-way match) · Statements ·
Clarifications. (Payment Vouchers = blank placeholder.)

## Recommended path (most reliable) — slice by slice
Run these in order, checking localhost after each (each is one Claude Code prompt from
BUILD-PLAN.md): **0 → 1 → 2 → 3 → 4 → 5 → 6 → 7 → 8 → 10**. Skip 9 (PV) and 11 (NetSuite).
After Slice 1 the app is already seeded with vendors/users; data grows each slice. Glance
at localhost between slices — that's what keeps it matching the prototype.

## ⭐ Recommended for a deadline — PHASED path (4 prompts, 4 checkpoints)

The efficient middle ground: batch related slices into 4 prompts. Run a phase, glance at
localhost (2 min), run the next. Foundation runs alone first because everything depends on
it. Each phase still tests against the prototype and seeds data.

Pre-flight (once): `docker compose up -d`, then keep two terminals open (API + web) so
hot-reload shows changes live.

**Phase 0 — Foundation (run alone, verify before continuing):**
> Read CLAUDE.md and all docs/. Run docker compose up if needed and scaffold per SETUP.md
> if the projects don't exist. Implement **Slice 0** (foundation + walking skeleton):
> AppDbContext (Npgsql), cross-cutting services (IClock, ICurrentUser, ICodeGenerator,
> IAuditLog + AuditEntry), a no-op INetSuiteClient stub, health endpoint, dev JWT login,
> a DataSeeder hook, first migration, and the React shell with the prototype's nav + a
> generated API client calling health. OBEY THE STANDING INSTRUCTION: test against
> prototype/eprocure-portal.html, iterate until dotnet test + npm test are green AND the
> shell/nav matches the prototype. Give me the localhost commands + verify checklist. Stop.

**Phase 1 — Master data + Requisitions/RFQ (Slices 1–2):**
> Implement **Slices 1 then 2** per BUILD-PLAN.md and SEED-DATA.md (dummy vendors + users
> incl. the approver; dummy requisitions; RFQ builder with all 12 field types). Seed the
> relevant demo data now. For EACH slice OBEY THE STANDING INSTRUCTION — test against the
> prototype and iterate until green AND localhost matches it; don't start Slice 2 until
> Slice 1 matches. Print localhost + a verify checklist after each. No mocks, no excuses.
> Stop after Slice 2.

**Phase 2 — Sourcing → Award (Slices 3–5), the demo centrepiece:**
> Implement **Slices 3, 4, 5** in order per BUILD-PLAN.md, BUSINESS-RULES.md, SEED-DATA.md
> (bidding + deadline; bid opening + technical eval + masking; commercial + award with
> eligibility/qty-cap/approval-DoA). Seed bids, technical scores with megatech FAILING, and
> the approved award + generated PO. For EACH slice OBEY THE STANDING INSTRUCTION — test
> against the prototype and run prototype/tests (fixes.js for deadline + eligibility),
> iterate until green AND matching; don't advance until the current slice matches. Print
> localhost + verify checklist after each. No excuses. Stop after Slice 5.

**Phase 3 — Procure-to-Pay tail (Slices 6, 7, 8, 10):**
> Implement **Slices 6, 7, 8, then 10** in order per BUILD-PLAN.md, BUSINESS-RULES.md,
> SEED-DATA.md (POs; ASN/GRN guards; Invoice 3-way match incl. one Exception and one Paid;
> Statements). Keep Payment Vouchers (Slice 9) a blank placeholder and NetSuite a stub.
> Seed POs across states, ASNs/GRNs, invoices, clarification threads, and audit entries.
> For EACH slice OBEY THE STANDING INSTRUCTION — test against the prototype and run
> prototype/tests (safety.js, e2e.js), iterate until green AND matching. Print localhost +
> verify checklist after each. No excuses. Stop after Slice 10.

After Phase 3, run the **Demo smoke check** below. If you run out of time, Phases 0–2 alone
give you the headline sourcing→award demo.

## Faster path (autonomous) — the demo mega-prompt
If you want Claude Code to run on its own through the in-scope slices, paste this. It will
still build, seed, and **test against the prototype after every slice** — but check
localhost at the checkpoints it prints.

> Read CLAUDE.md and ALL docs/ (BUILD-PLAN, BUSINESS-RULES, DATA-MODEL, ARCHITECTURE,
> CONVENTIONS, PROTOTYPE, SEED-DATA). We are preparing a CLIENT DEMO. Build the in-scope
> slices **in order: 0,1,2,3,4,5,6,7,8,10** (skip Slice 9 Payment Vouchers — leave a blank
> placeholder; skip Slice 11 NetSuite — keep the stub). For EACH slice: restate it, build
> DB+API+React+tests, seed the demo data from SEED-DATA.md, then OBEY THE STANDING
> INSTRUCTION — test against prototype/eprocure-portal.html (open it / read its <script> /
> run prototype/tests), and keep iterating until `dotnet test` + `npm run test` are green
> AND the localhost behaviour MATCHES the prototype for that slice. Do NOT move to the next
> slice until the current one matches the prototype. After each slice, print: what's done,
> the localhost URL/commands, and a 3-line "what to click to verify" checklist, then
> continue. Seed ALL data in SEED-DATA.md (SWEC tree, item master, users incl. the
> approver, 10 vendors with tab data, PRs, forms, the 4 RFQs across states, bids, technical
> scores with megatech failing, the approved award + PO, POs across states, ASNs/GRNs,
> invoices incl. one Exception and one Paid, clarification threads, audit entries) so every
> screen is populated. No mocks, no excuses, no "coming soon" for in-scope features — it
> must work for real on localhost and match the prototype. Stop only when 0–8 and 10 are
> all done and matching.

## Demo smoke check (do this before the client)
1. Dashboard numbers match the prototype (1 awaiting bids, 1 ready to open, 1 under eval).
2. Vendor Master: open a vendor, tabs populated (banking/certs/SWEC/performance/contacts).
3. RFQ-0087 (Open): submit a bid as a vendor → succeeds; move ClosesUtc to the past →
   submit is rejected (deadline rule).
4. RFQ-0079 (Evaluation): megatech shows FAIL and is **not** selectable for award.
5. Award needs **Lim** (not Faridah) to approve → then a PO is generated (SoD/DoA).
6. PO-1185: ASN → GRN → Invoice shows a green 3-way match; PO-1193 invoice shows
   **Exception** and cannot be paid.
7. Statements: a vendor ledger shows the paid + outstanding lines.
8. Clarifications: the RFQ-0087 broadcast answer appears for all bidders.

If all 8 pass, you're demo-ready.
