# PROMPT — CF-FINAL: three slices, unattended, correctness-first (handover tomorrow)

You are Claude Code on eProcure. The operator is away for several hours. Run THREE slices to
completion unattended: **Slice 1 (functional fixes) → Slice 2 (UI polish) → Slice 3 (preferred
forms + cleanup).** Every gate is self-verification against a FIXED checklist — no human to confirm.

Read first, in order:
1. `docs/CF-FINAL/PROGRESS-LEDGER.md` — THE ANTI-DRIFT CHECKLIST. You tick boxes + fill Evidence; you
   NEVER reword an item. It's your acceptance artifact AND resume mechanism.
2. `docs/CF-FINAL/UI-POLISH-REFERENCE.html` — the operator-greenlit design for Slice 2. Match that
   feel exactly: square + bold KEPT, movement softened. Tokens are in the file.
3. `CLAUDE.md`, `CHARTER.md`, and for Slice 1: `PrForm.tsx`, `EntryFormService.cs`,
   `Requisition.cs` (domain), the transaction create screens.

## THE PRIME DIRECTIVE — correctness ships even if polish stalls
**Slice 1 is the only thing here that fixes something BROKEN (a PR silently reverts its form and
shows phantom fields). It MUST be committed and green before anything else.** If Slices 2 or 3 hit
trouble, the handover still has the bug fixed. So: **hard sequence, no skipping ahead.** Slice 1 all
green → tag `v2.1-fixes` → Slice 2 → tag `v2.2-ui-polish` → Slice 3 → tag `v2.3-final`.

## ANTI-DRIFT PROTOCOL (how you stay on-spec unattended)
1. **After EVERY task commit:** open the ledger, tick the box(es), fill Build (commit hash) + Test
   (browser test name), append a UTC-timestamped line to `docs/CF-FINAL/PROGRESS.md`. Then **re-scan
   every remaining unticked box in the current slice** — confirm you skipped nothing.
2. **After EVERY slice (the SLICE GATE):** re-read that slice's section of THIS prompt (not just the
   ledger — go to the source so a transcription miss can't hide a gap). Confirm every box is `[x]` or
   in `BLOCKERS.md`. Only then tag and start the next slice.
3. **Before declaring done:** walk EVERY box across all three slices. Each is `[x]` or blocked. A box
   with no tick and no blocker means you're not done — build it.
4. A capability is done ONLY when a **Playwright browser test drives it and passes** — this is a UX
   round; an API/unit test alone never ticks a UI box.

## AUTONOMY DISCIPLINE
- **One atomic commit per task**, prefixed `CFF-Tn:`. ALL gates green before each (dotnet test,
  vitest, tsc, oxlint 0). A red suite blocks the commit — fix, then commit.
- **Hold-or-raise** the baselines (dotnet 541 / vitest 251 / e2e 98) every commit.
- **Log-and-continue, never halt the whole run:** a task that can't pass after genuine effort → write
  it to `docs/CF-FINAL/BLOCKERS.md` (what, why, what you tried), leave it, move on. One stuck task
  must not cost the rest — ESPECIALLY don't let a Slice 2/3 snag block the already-green Slice 1.
- **Small decisions not covered here:** pick the option most consistent with the ledger + the UI
  reference + prior CF rounds, record it in `docs/CF-FINAL/AUTO-DECISIONS.md` (decision, why, how to
  reverse), continue.
- **Detach long commands, poll, use timeouts;** restart PG/servers if they drop. Atomic commits are
  recovery points — a fresh low-context session reads the ledger + git log + PROGRESS.md and resumes
  at the first unticked box.
- **Hard boundaries:** never touch Payment Vouchers, Contract Mgmt, NetSuite integration,
  ApprovalThresholdMyr. The L3 Header invariant, L6 data-safety, OD-D7-2, and the anti-rot patterns
  (reference registry loops over providers; central archived-value filter; EntryFormField sole
  placement authority) remain HARD rules — do not regress them for a UI change.
- **Migrations:** T1 (`RequisitionChosenForm`) and possibly T8 are the only expected ones. An
  unplanned migration → STOP that task, log it, continue.

## THE THREE SLICES (full detail in the ledger; this is the spine + the rulings)

**SLICE 1 — FUNCTIONAL FIXES (do first, ship-critical):**
- **T1 — the form-persistence bug (verified root cause):** `chosenFormId` lives only in React state
  and there is NO form field on the Requisition record, so the pick is never saved; on reopen it
  defaults to Standard and the view dumps every applicable custom field/segment below the sublist.
  FIX: add `EntryFormId Guid?` to Requisition (migration `RequisitionChosenForm`), persist on save,
  resolve the saved form on reopen (init `chosenFormId` from the loaded record), and REMOVE the
  ungoverned fallback so only the chosen form's placements render. Keep OD-D7-2 at submit.
- **T2 — custom fields → custom forms only:** server-refuse placing a custom field on an `IsSystem`
  standard form (standard forms stay source/seed-controlled); the field-creation picker offers only
  custom forms.
- **T3 — Est. Amount = qty × rate** next to Est. Rate on the standard PR line grid, currency-grouped.

**SLICE 2 — UI POLISH (match `UI-POLISH-REFERENCE.html`):**
- **T4 — one global toast + save-banner provider**, wired to EVERY save across the app. Copy: past
  tense, no "successfully", no "!".
- **T5 — standardize Save-button placement** top-right of every create/edit panel (transactions AND
  admin config).
- **T6 — transitions + skeleton loaders:** row-hover settle, button press-scale, skeletons replace
  blank flashes, soften the hard jumps (form-builder open, save→view).
- **T7 — header title Georgia regular weight** (not bold).
- **Scope discipline:** this slice is the shared polish LAYER (a toast provider, a skeleton component,
  transition CSS, save-button placement). Do NOT attempt a ground-up NetSuite-pixel rebuild of the
  form builder — that's out of scope for this run. The polish layer carries the fluid feel; a full
  restyle is post-handover.

**SLICE 3 — PREFERRED FORMS + CLEANUP:**
- **T8 — preferred form by role/all-roles + defaulting:** expose + persist the preferred setting; the
  transaction picker defaults to the role's preferred form (Standard when none). The resolver already
  does role precedence — build on it, don't reinvent.
- **T9 — dummy-data cleanup:** delete the bulk of `FIX4/FIX5/BUDGET REF nnn/Site Ref nnn/Partner N`
  litter; leave a FEW curated realistic ones (e.g. "Cost Centre", "Payment Terms", "Project") so
  handover shows plausible config. Use the governed lifecycle (inactivate/delete/purge with audit
  snapshot) where records reference them — never orphan.

## MORNING REPORT
`docs/CF-FINAL/CF-FINAL-REPORT.md`: lead with Slice 1 (the bug fixed, root cause + proof), then
Slices 2-3; the AUTO-DECISIONS list front and center ("here are the N calls I made — veto any");
BLOCKERS (ideally empty); full-suite pass counts; five-lens review; a line-by-line map of the
operator's 9 findings → commit → browser proof. Tags `v2.1-fixes`, `v2.2-ui-polish`, `v2.3-final`.

Begin now with SLICE 1, T1 — the bug fix. Correctness first. Reconcile after every task and every
slice. Do not wait for the operator — self-verify, log, keep going.
