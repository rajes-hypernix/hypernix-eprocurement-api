# PR Module + Consolidate Lines — Specification

> Behaviour source of truth: `prototype/eprocure-pr-module-mockup.html`.
> Style source of truth: the existing app's design tokens / primitives (NOT the mockup's hex/fonts).
> This spec is additive to the current build. The existing **Confirm lines** flow stays as-is.

---

## 1. Why this exists

Today PRs are read-only seed data and the only sourcing path is the one-click **Confirm
lines** flow. This module adds three things:

1. **PR database + portal PR creation/editing** — users raise and edit PRs in the portal;
   seeded PRs become normal, openable, editable records.
2. **Consolidate Lines** — a multi-PR "line basket" builder for the power-user case (pull
   open lines from across several PRs into one RFQ, with merge + provenance).
3. **PR line lifecycle** — cancel a line, release a line for re-sourcing, re-open a cancelled
   line — all audited, all preserving provenance for the future analytics layer.

---

## 2. Data model changes (backend)

Current state (do not break): `PurchaseRequisition` holds a `List<PrLine>`; `PrLine` has
`ItemCode, Description, Qty, Uom, EstUnitPrice, Status ("available"|"rfq"|"awarded"), Ref?`.
`PrLine` has **no stable identity**. `Rfq` has `RfqLine` children with no stable line code.

### 2.1 `PrLine` gets a stable identity — REQUIRED FIRST
Add `public Guid Id { get; private set; } = Guid.NewGuid();` to `PrLine`. Migrate seed data
so every existing seeded line gets an Id. **Nothing else in this module works without this.**

### 2.2 `PrLineStatus` (lifecycle) — clean enum
```
enum PrLineStatus { Open, InDraftRfq, InRfq, Awarded, Cancelled, Closed }
```
Mapping from existing string values so nothing breaks:
`available → Open`, `rfq → InRfq`, `awarded → Awarded`.
Keep a string column OR introduce the enum — your call — but the **stored set is exactly
these six**. Notably, **"No quotes" is NOT a stored status.** A line that came back from an
RFQ is simply `Open` again; the "No quotes / returned" chip is *derived* from its most recent
`PrLineSourcing` link having `LinkStatus = Returned` (+ reason). Keeping it derived keeps the
enum clean for analytics. (The mockup shows a `noquote` display state — implement it as
derived, not as a 7th stored value.)

### 2.3 `PrLineSourcing` — the provenance link table (SOURCE OF TRUTH)
```
PrLineSourcing {
  Guid Id
  Guid PrLineId            // FK -> PrLine.Id
  Guid RfqId               // FK -> Rfq.Id
  string RfqLineCode       // stable code of the RFQ line this PR line fed
  decimal QtySourced       // = full PR line qty in v1 (schema ready for partial later)
  LinkStatus LinkStatus    // Active | Returned | Cancelled
  string? Reason           // captured on Returned/Cancelled
  DateTime CreatedUtc
  DateTime? ClosedUtc
}
enum LinkStatus { Active, Returned, Cancelled }
```
Rules:
- A **merged** RFQ line = **multiple** `PrLineSourcing` rows (one per source PR line).
- Links are **never deleted.** Returning/cancelling sets `LinkStatus` + `ClosedUtc` + `Reason`.
- Provenance is never lost on merge, reorder, or return. This table is what the analytics
  layer will read.

### 2.4 `RfqLine` gets a stable code/id — REQUIRED for the link
`PrLineSourcing.RfqLineCode` must point at something stable. Add a stable `LineCode`
(or `Guid Id`) to `RfqLine` if absent.

### 2.5 PR header status — DERIVED, recomputed on every line change
```
PR header status =
  all lines Cancelled                          -> Cancelled
  any line Open AND any line InRfq/Awarded      -> PartiallySourced
  no Open line AND any line InRfq/Awarded       -> Sourced
  has Open lines, none sourced yet, submitted   -> Submitted
  portal-created, not yet submitted             -> Draft
```
Implement a `RecomputePrHeaderStatus(pr)` server-side helper called after every line
transition (mirror `recomputePr` in the mockup). Keep the existing `Status` field working;
add the derived header status alongside it (do not break existing readers/seed).

### 2.6 Permission / concurrency surfaces — TRACK, don't build yet
Write surfaces that the later hardening pass must guard: `create-PR`, `edit-open-line`,
`cancel-line`, `release-line`, `source-line`, `merge-lines`, `create-RFQ-from-basket`.
New write rows (`PrLineSourcing`, PR edits) are `RowVersion` candidates. v1: allow the buyer
role (matching current demo). Just leave a `// HARDENING:` marker at each surface.

---

## 3. Line lifecycle (the heart of the module)

```
            ┌─────────── source into RFQ (add to basket / confirm) ──────────┐
            │                                                                 ▼
   Cancelled ◄── cancel ── Open ── add to draft basket ──► InDraftRfq ── release RFQ ──► InRfq
        │ ▲                  ▲  ▲                              │                            │
   re-open │                 │  │ abandon draft RFQ ──────────┘                            │
        └──┘                 │  └──────────────── return (RFQ cancelled) ──────────────────┤
                             │                                                              │
                             │◄── released for re-sourcing (no quotes / not awarded) ───────┤
                             │                                                              ▼
                             └────────────────────── (loses) ◄──────── award ─────────► Awarded (terminal v1)
```

Transitions and what each does:

| From | Action | To | Side effects |
|---|---|---|---|
| Open | **Cancel line** (reason) | Cancelled | AuditEntry; demand withdrawn (terminal) |
| Open | **Add to basket** | InDraftRfq | soft reservation; no link yet |
| InDraftRfq | **Release RFQ** | InRfq | write `PrLineSourcing` (Active), per-source qty for merges |
| InDraftRfq | **Abandon/delete draft RFQ** | Open | reservation released; no link written |
| InRfq | **RFQ cancelled** | Open | link → Cancelled (reason); line re-sourceable |
| InRfq | **Award: line wins** | Awarded | link stays Active; **terminal in v1** |
| InRfq | **Award: line loses / not awarded** | Open | link → Returned (reason "not awarded / residual") |
| Open (was returned) | **Release for re-sourcing** (reason) | Open | shown as "No quotes" chip via last Returned link; can re-source or Cancel |
| Cancelled | **Re-open** | Open | AuditEntry; PR header recomputed |

Every transition writes an `AuditEntry`. Cancel + release capture a **reason**.

---

## 4. Screens (additive)

### 4.1 Requisitions page → becomes the PR List
Keep the existing page route. Add:
- **Table / Kanban view toggle** (mirror the RFQ list's toggle pattern). Kanban columns by PR
  header status: Draft, Submitted, Partially sourced, Sourced, Cancelled. (`prKanban` in mockup.)
- **Create PR** button (top-right, aligned with the toggle — see alignment rules in UI-FIXES).
- **Expandable PR rows** — expanding a PR shows its lines with per-line state chips and the
  per-line actions from §3 (Cancel line / Release for re-sourcing / Re-open / locked hints).
- **Keep the existing filters** (Requestor, Dept, Location, Job, Category, Show) and the
  **existing "Confirm lines"** action — untouched.

### 4.2 Create / Edit PR form
Same fields as a NetSuite-origin PR (header: Requestor, Department, Category, Location,
Job/Cost ref, Required by, Memo; lines: Item code, Description, Qty, UoM, Est. rate).
- New PR: **Save draft** / **Submit PR**. Code generated from server sequence on save.
- Edit existing PR: **Save changes**; if status is Draft/Submitted, a **Source these lines**
  shortcut into the Consolidate workspace.
- **Editability:** header + **Open** lines editable; **InRfq/Awarded** lines render read-only
  with a lock chip. New lines addable to any non-cancelled PR.
- **Cancel PR** only when no line is InRfq/Awarded (reason captured).
- Seeded PRs use this *same* form. Nothing special about portal-created vs seeded once it exists.
(See `drawForm`, `formSave`, `formCancelPr` in mockup.)

### 4.3 Consolidate Lines workspace (additive entry point)
Two-pane "line basket" builder for multi-PR sourcing.
- **Left pane:** PRs grouped, showing **open lines only** (InRfq/Awarded/Cancelled lines do
  not appear at all). Filters: Dept, Category, Location (multi-select) + free-text search
  (item, code, PR, requestor). "Add all" per PR; `+` per line.
- **Right pane (basket):** chosen lines; `−` remove, **up/down** resequence, running totals
  (lines, # PRs, est. value).
- **Merge:** adding a line whose **item code already in the basket**:
  - same item code, **same UoM** → prompt **Merge** (sum qty, keep provenance) or **Keep separate**.
  - same item code, **different UoM** → cannot sum; auto **keep separate** with a note.
  A merged basket line shows an expandable **provenance** list (which PR lines + per-source qty).
- **Build RFQ:** hands the basket into the **existing `RfqBuilder` wizard.** On release, each
  basket line writes `PrLineSourcing` (per-source qty for merges) and flips source PR line(s)
  to `InRfq`. **Adding to the basket** flips them to `InDraftRfq` (soft reservation).
(See `renderConsolidate`, `clAdd`, `clDoMerge`, `clKeepSeparate`, `clMove`, `clToggleProv`,
`clProceed` in mockup.)

---

## 5. Integration hooks into existing services (additive, well-tested)

1. **Award finalisation (`AwardService`):** after an award is finalised, for every sourced PR
   line **not** awarded, set its Active link → `Returned` (reason "not awarded / residual")
   and flip the PR line to `Open`. Awarded lines → `Awarded`, link stays Active. Must not
   change existing award behaviour or break existing award tests — add new tests.
2. **RFQ cancel:** cancelling an RFQ returns all its `InRfq`/`InDraftRfq` lines to `Open`
   (links → Cancelled, reason). Wire into the existing RFQ cancel path.
3. **Abandoned draft RFQ:** deleting/discarding a draft RFQ that holds `InDraftRfq` lines
   returns them to `Open` (no link was written).

---

## 6. Seed data (for the demo — show every state)

Keep all existing seeded PRs (openable/editable). Add seeds so the demo shows the full
lifecycle: one PR with an **Open** line + a **returned/"no quotes"** line, one
**Partially sourced** PR, one **fully Awarded** PR, one **Cancelled** PR, and a couple of PRs
sharing the **same item code** (one pair same-UoM for the merge demo, one pair different-UoM
for the keep-separate demo).

---

## 7. Out of scope for v1 (track, don't build)

- **Partial-quantity sourcing UI** (schema is qty-capable; UI sources whole lines).
- **Awarded-line rebid** (needs PO cancel — not built). Awarded is the v1 terminal state.
- **Cross-PR validation** (e.g. differing required dates) and **same-item auto-suggestions.**
- **Concurrency hardening** (RowVersion) — markers only.
