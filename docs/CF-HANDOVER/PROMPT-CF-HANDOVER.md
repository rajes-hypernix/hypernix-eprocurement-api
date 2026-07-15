# PROMPT — CF-HANDOVER: clean data, real config, standardized admin (unattended, handover-critical)

You are Claude Code on eProcure. **Handover is imminent.** Run all seven tasks unattended, in order,
with speed AND accuracy. Every gate is self-verification against a fixed checklist — no human to ask.

Read first:
1. `docs/CF-HANDOVER/PROGRESS-LEDGER.md` — THE FIXED CHECKLIST. Tick boxes + fill Evidence; never
   reword an item. It's the acceptance artifact and the resume mechanism.
2. `CLAUDE.md`, `CHARTER.md`; for T6 the existing **Entry Forms** screen (`AdminEntryForms.tsx`) — it
   already implements the list → Open → full-page → Back pattern the other admin screens must copy.
3. `docs/CF-FINAL/UI-POLISH-REFERENCE.html` — the design language (square + bold, softened movement).

## THE SEQUENCING INSIGHT (this is why the order matters — do not reorder)
**T1 unlocks T2.** Last round's cleanup could only *deactivate* the litter custom fields (83 on
Purchase Order alone) because the data-safety guard rightly refuses to delete a field that holds live
values — so the admin screens are now a wall of "Deactivated" badges, which is NOT the clean result the
operator asked for. **Delete the test transactions FIRST (T1) → the litter fields lose all their values
→ they become genuinely hard-deletable (T2).** Data before UI. T1–T5 are the handover value; T6/T7 are
the acceptable casualties if time runs short.

## SCOPING RULINGS (decided — build to these, don't re-litigate)
- **T4 Item Master — minimal and safe.** `ItemCode` is a plain string on EVERY line entity (PR, PO,
  Bid, Invoice, Delivery). **Do NOT re-key those lines** — that's a wide, risky migration hours before
  handover. Instead: `Item` is a small master used as a **lookup source**; lines keep storing `ItemCode`
  as a string; the PR entry form **picks** from the master and **auto-fills Description + UoM**. This
  delivers "item is a list, not free text" with zero line-table migration.
- **T3 native fields → segment-backed.** `PurchaseRequisition` already has companion `*Code` columns
  (`DepartmentCode`, `LocationCode`, `CategoryCode`, `JobCode`) alongside the label strings — the design
  anticipated coded values. Use them: the picker writes label + code. No new columns needed.
- **T5 seed, not just DB.** Last round's cleanup ran against the dev database only, so a fresh instance
  would resurrect the litter. Everything curated in T1–T4 must land in the **seed**
  (`DevelopmentDataSeeder`, `Seed/*`) so handover reproduces the clean state on a fresh DB.
- **T2 is DELETE, not deactivate.** The whole point is a clean screen. If something legitimately can't
  be deleted (a real reference survives), log it — but the expectation after T1 is that the litter is
  value-free and goes.

## ANTI-DRIFT PROTOCOL
1. **After EVERY task commit:** tick the ledger box(es), fill Build (hash) + Test (browser test name),
   append a UTC line to `docs/CF-HANDOVER/PROGRESS.md`, then re-scan remaining unticked boxes.
2. **After every task:** re-read that task's section of THIS prompt (the source, not just the ledger)
   before starting the next — a transcription miss must not hide a gap.
3. **Before declaring done:** walk every box. `[x]` or a BLOCKERS entry. No silent gaps.
4. A capability is done only when a **browser test drives it and passes** (UI tasks) or a verified
   data assertion proves it (data tasks). Not "the code looks right."

## AUTONOMY DISCIPLINE
- One atomic commit per task, `CFH-Tn:`. ALL gates green before each (dotnet test, vitest, **tsc -b**
  — note: `tsc --noEmit` is VACUOUS against the root config, `files: []`; only `-b` is a real check —
  and oxlint 0). Hold-or-raise 550/256/117.
- **Log-and-continue, never halt:** a stuck task → `BLOCKERS.md` (what, why, tried), move on. Never let
  a T6/T7 snag cost the T1–T5 handover value.
- Small decisions not covered → pick what's most consistent with the ledger + prior CF rounds, log to
  `AUTO-DECISIONS.md` (decision, why, reverse), continue.
- **Hard boundaries:** never touch Payment Vouchers, Contract Mgmt, NetSuite integration,
  ApprovalThresholdMyr. The L3 Header invariant, L6 data-safety, OD-D7-2, and the anti-rot patterns
  (reference registry loops over providers; central archived-value filter; EntryFormField sole placement
  authority) stay HARD. Expected migrations: `ItemMaster` (T4) only — anything else, STOP and log.
- Detach long commands, poll, use timeouts. Atomic commits are recovery points — a fresh session reads
  the ledger + git log + PROGRESS.md and resumes at the first unticked box.

## THE SEVEN TASKS (detail in the ledger; this is the spine)
- **T1** — purge ALL test transactions, FK-ordered (Invoice → Grn → Asn → PO → Award → Bid →
  RfqInvitation/Event → Rfq → PrLineSourcing → PR, with lines/custom-values/segment-assignments). Zero
  orphans.
- **T2** — now hard-DELETE the litter config: custom fields (`CF4 Star *`, `Fix3/4/5 *`, `Budget
  Cap/Ref *`, `Site Ref *`, `Partner N`), lists (`CF List *`, `Fix1 Values *`), segments (`Project
  nnnnnn`, and specifically **`Project Code Grouped`** — the operator named it), litter saved views.
  Keep System views. Result: clean screens, **no wall of Deactivated badges**.
- **T3** — create **Department, Location, Project, Category** segments, 5 realistic O&G procurement
  values each, applied Header AND Line, standard on Requisition + other transactions; convert the PR's
  free-text Department/Location/Category/Job into segment-backed searchable pickers (write label+code).
- **T4** — **Item Master** (`ItemCode`, `Description`, `UoM`), admin screen, ~10 realistic items; PR line
  Item Code becomes a searchable picker that auto-fills Description + UoM. Per the ruling above: lookup
  source only, no line re-keying.
- **T5** — seed **~10 realistic sample PRs** using the new items + segments, spread of statuses; and put
  ALL curated config into the **seed** so a fresh DB comes up clean.
- **T6** — Custom Lists, Custom Fields, Segments, Numbering all adopt the **Entry Forms pattern**: list
  → Open → full page → Back (with the real dirty-state guard). Custom Fields' RECORD TYPE column shows
  all applied types comma-separated ("Requisition, Purchase Order").
- **T7** — the New Saved View modal's pickers (Record Type, Field, Operator, Add Column) become the
  typeable `SearchSelectField`.

## REPORT
`docs/CF-HANDOVER/CF-HANDOVER-REPORT.md`: lead with the before/after counts (transactions, fields,
lists, segments, views), then per-task; AUTO-DECISIONS front and centre; BLOCKERS (ideally empty);
suite counts; five-lens review; the operator's 5 asks → commit → proof. Tag `v2.4-handover`.

Begin now with T1. Data before UI. Reconcile after every task. Do not wait for the operator.
