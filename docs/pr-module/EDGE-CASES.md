# Edge Cases — expected behaviour (assert all of these)

Each row is a test. Behaviour mirrors `prototype/eprocure-pr-module-mockup.html`.

## A. Line lifecycle

| # | Scenario | Expected |
|---|---|---|
| A1 | Cancel an **Open** line | Line → Cancelled (terminal); reason stored; AuditEntry; PR header recomputed |
| A2 | Re-open a **Cancelled** line | Line → Open; AuditEntry; if PR was Cancelled it flips back to Submitted/PartiallySourced |
| A3 | Add Open line to basket | Line → InDraftRfq (soft reservation); no link row yet |
| A4 | Abandon a draft RFQ holding InDraftRfq lines | Those lines → Open; no link written |
| A5 | Release RFQ with basket lines | Each → InRfq; one `PrLineSourcing` (Active) per source line; merged line writes one link **per source** |
| A6 | RFQ awarded, line **wins** | Line → Awarded; link stays Active; terminal in v1 |
| A7 | RFQ awarded, line **quoted but lost / not awarded** | Line → Open; its Active link → Returned, reason "not awarded / residual" |
| A8 | RFQ where **no vendor quoted a line** | Line returns to Open; chip shows "No quotes"; user may Release for re-sourcing or Cancel |
| A9 | RFQ **cancelled** entirely | All its InRfq/InDraftRfq lines → Open; links → Cancelled with reason |
| A10 | Release a returned line for re-sourcing | Line stays Open and is selectable again; reason captured; provenance preserved |
| A11 | Try to cancel an **InRfq** line directly | Blocked; UI shows "Locked to RFQ (live)"; no transition |
| A12 | Try to cancel an **Awarded** line | Blocked; "Awarded — PO issued"; rebid is deferred scope |

## B. PR header status (derived)

| # | Lines state | Header |
|---|---|---|
| B1 | All Cancelled | Cancelled |
| B2 | Some Open + some InRfq/Awarded | Partially sourced |
| B3 | No Open + some InRfq/Awarded | Sourced |
| B4 | Has Open, none sourced, submitted | Submitted |
| B5 | Portal-created, not submitted | Draft |
| B6 | Re-opening a line in a Cancelled PR | Recompute flips header off Cancelled |

## C. PR create / edit

| # | Scenario | Expected |
|---|---|---|
| C1 | Create PR (Save draft) | New PR, status Draft, **code from server sequence**, AuditEntry |
| C2 | Create PR (Submit) | Status Submitted; lines default Open |
| C3 | Edit header of any non-cancelled PR | Allowed; AuditEntry |
| C4 | Edit an Open line | Allowed |
| C5 | Edit an InRfq/Awarded line | Blocked; renders read-only with lock chip |
| C6 | Add a new line to a Partially-sourced PR | Allowed (new line is Open) |
| C7 | Cancel PR while a line is InRfq/Awarded | **Blocked**; Cancel PR button hidden/disabled |
| C8 | Cancel PR when all lines Open/Cancelled | Allowed; open lines → Cancelled; header → Cancelled; reason |
| C9 | Open a **seeded** PR | Opens in the same edit form; behaves identically to portal-created |

## D. Consolidate Lines + merge/provenance

| # | Scenario | Expected |
|---|---|---|
| D1 | Left pane contents | Open lines only; InRfq/Awarded/Cancelled lines are **absent** (not greyed — absent) |
| D2 | Add same item code, **same UoM** | Prompt: Merge (sum qty, keep provenance) or Keep separate |
| D3 | Add same item code, **different UoM** | Auto keep-separate + note "can't sum, kept as separate lines" |
| D4 | Merge two PR lines | One basket line; provenance list shows both source PR lines + per-source qty |
| D5 | Resequence basket (up/down) | Order changes; ends disabled at top/bottom |
| D6 | Remove a basket line | Removed; if it was InDraftRfq-only, source line returns to Open |
| D7 | "Build RFQ" | Hands basket into the **existing** RfqBuilder wizard; no second wizard created |
| D8 | Release the built RFQ | Source lines → InRfq; links written; merged line → multiple links |
| D9 | Filter + search | Dept/Category/Location multi-select + free-text over item/code/PR/requestor |

## E. Concurrency / integrity (v1 behaviour + markers)

| # | Scenario | Expected |
|---|---|---|
| E1 | Two drafts try to hold the same Open line | Add reserves to InDraftRfq; on release, server **re-checks** the line is still claimable by THIS rfq; otherwise blocks with a clear message |
| E2 | Release an RFQ whose line was cancelled meanwhile | Blocked; message; no partial commit |
| E3 | Links never deleted | Returned/Cancelled set LinkStatus + ClosedUtc + Reason; row retained |

## F. Must-not-regress (existing behaviour)

| # | Assert |
|---|---|
| F1 | Existing **Confirm lines** flow works unchanged |
| F2 | Existing RFQ builder, bid, evaluation, award, PO, GRN, invoice, SOA flows unchanged |
| F3 | All pre-existing tests stay green |
| F4 | Existing Requisitions filters still work |

## G. E2E (headline flow)
Create PR → submit → Consolidate (pull lines from 2 PRs, merge a same-UoM pair) → Build RFQ
via existing wizard → release → (award none / no quotes) → lines return to Open → re-source
into a new RFQ → award one → that line Awarded, the rest still Open. Assert provenance links
at every step.
