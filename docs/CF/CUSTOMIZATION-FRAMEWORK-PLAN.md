# eProcure Customization Framework — NetSuite Parity Analysis & Build Plan

**Verification standard:** every finding below was tested by **driving the actual browser**
(Playwright, screenshots in `/tmp/probe/`) and by hitting the live API as the correct persona
— not by reading code and inferring. Where my first read was wrong, I've said so. This is the
document your build plan and the final line-by-line comparison test against.

**How to read the tables:** each row is a function from your document, what **NetSuite** does,
what eProcure does **today** (browser-verified), and the **verdict** — ✅ works / ⚠️ partial
(model exists, UI incomplete) / ❌ missing.

---

## Correcting the record

In my first analysis I told you several things were "already there" by reading the database
schema. You were right to reject that. A `SavedViewId` column that no screen can populate is
not a feature — it's a buried capability. Here's what the **browser** actually shows, and it's
a genuine mix, neither "all there" nor "all missing":

- **Reminders CAN bind a saved view** — the modal exists and works — **but** the picker shows
  essentially one option because only one buyer saved view is seeded, so it *reads* as empty.
  Your experience was correct; my "it works" was hollow.
- **Custom fields CAN be edited** in the UI (I initially mis-tested this with an incomplete
  request body and got a false 400 — my error, corrected).
- **Custom lists CANNOT be edited** — no rename, no description edit, no order-mode. Your
  complaint is literally true at the API level (no `PUT /custom-lists/{code}` exists).
- **Dashboard drag-drop, portlet-removal, and tile authoring genuinely do not exist** —
  browser-confirmed: 0 draggable elements, 0 remove buttons, 0 add-shortcut controls.
- **The entry-form "customizer" is a form *list*, not a layout editor** — no subtab authoring,
  no field groups, no show/hide/reorder grid, no sublist picker. This is the widest gap.
- **Global search misroutes** — clicked `PR-2026-0412`, landed on the Requisitions **list**
  (`hash: reqs`), not the PR. Confirmed.

---

## 1. CUSTOM FIELDS (body)

**NetSuite reference (your screenshots):** Label · ID (auto or manual) · Type (free-form,
date, datetime, long text, list, multi-select, checkbox, currency, …) · options (store value,
show in list, global search, encrypted) · Applies-To (which transactions) · Display type
(normal/disabled/inline) · Help · Subtab · Insert-before · Validation/mandatory · Default.

| Function | NetSuite | eProcure today (verified) | Verdict |
|---|---|---|---|
| Label | ✓ | ✓ (edit modal) | ✅ |
| ID / internal id | auto from name, manual override | auto `cf_*`, immutable | ⚠️ no manual override |
| Type list | ~15 types incl currency, multiselect, datetime | 8 (Text, LongText, Int, Decimal, Money, Date, Bool, ListValue) | ⚠️ no multiselect, no datetime |
| **Create** | ✓ | ✓ | ✅ |
| **Edit after create** | ✓ | ✓ (label/help/required/sort; type immutable) | ✅ |
| **Inactivate** | ✓ | ✓ (`/active`) | ✅ |
| **Delete (if unused)** | ✓ | ✓ (hard-delete when zero values) | ✅ |
| Display type (normal/disabled/inline) | ✓ | ❌ not in modal (verified: modal has no "display") | ❌ |
| Applies-to (which transactions) | multi-select of record types | one RecordType per def | ⚠️ single, not multi |
| Help text | ✓ | ✓ | ✅ |
| Subtab placement | on the field def | only via entry-form placement, not the def | ⚠️ different model |
| **Insert-before** (named field) | ✓ | ❌ only integer sort | ❌ |
| Mandatory | ✓ | ✓ (Required) | ✅ |
| Default value | ✓ | ✓ on form placement (@tokens) | ✅ |
| Store value | checkbox | always stored | ⚠️ n/a (fine) |
| **Show in list** (column in list views) | ✓ | ❌ | ❌ (ties to SavedView) |
| Global search / Encrypted | ✓ | ❌ | ❌ (recommend DROP — low value) |
| **Save & apply to forms** | ✓ | ✓ (D7 placement) | ✅ |

**Gaps to build:** display-type on the field, insert-before picker, show-in-list flag,
multi-record applies-to (optional), multiselect/datetime types (optional).

## 2. CUSTOM LINE FIELDS (columns) — ❌ ENTIRELY MISSING

**NetSuite:** custom *column* fields = transaction line fields, same authoring surface as body
fields but scoped to sublist lines.

**eProcure today:** `CustomFieldValue` is keyed by `(RecordType, RecordId)` — **there is no
`LineId`**. No line-scoped custom fields exist at any layer. **This is genuine new
architecture** (line value table + line placement + sublist render). Couple it with §6.

## 3. CUSTOM LISTS

**NetSuite:** name · ID · description · order (entered vs alphabetical) · values (auto-id) ·
full edit/inactivate/delete on both list and values.

| Function | NetSuite | eProcure today (verified) | Verdict |
|---|---|---|---|
| Create list | ✓ | ✓ | ✅ |
| **Edit list (rename/description)** | ✓ | ❌ **no `PUT /custom-lists/{code}`** | ❌ |
| **Order mode (entered/alphabetical)** | ✓ | ❌ no flag (only integer sort) | ❌ |
| Inactivate/delete list | ✓ | ❌ no endpoint | ❌ |
| Add value | ✓ | ✓ | ✅ |
| Edit value | ✓ | ✓ (verified: 14 edit-value controls) | ✅ |
| Delete value (guarded) | ✓ | ✓ (A2F added in-use guard) | ✅ |
| Value auto-id | ✓ | ✓ | ✅ |

**Gaps to build:** edit-list (rename/description), order-mode flag, inactivate/delete-list.

## 4. ENTRY FORMS — ⚠️ the "customizer" is a list, not an editor (WIDEST GAP)

**NetSuite:** "Customize Form" opens a full layout editor — customize-existing / save-as-new,
show/hide subtabs, **create field groups** (body + subtab), per-field show/hide/reorder/**column
break**/"same as previous", **pick which sublist** shows in a subtab, show/hide/reorder line
fields.

| Function | NetSuite | eProcure today (verified in browser) | Verdict |
|---|---|---|---|
| Customize existing / save-as-new | ✓ | ✓ (New/Edit/Copy buttons on the form list) | ✅ |
| Per-role form resolution | (roles) | ✓ (D7) | ✅ |
| Place body field on subtab | ✓ | ✓ (`Subtab` on placement) | ✅ |
| **Create field group** | ✓ | ❌ (verified: no "field group" in UI) | ❌ |
| **Show/hide subtabs** | ✓ | ❌ | ❌ |
| **Create subtab (as object)** | ✓ | ❌ (verified: no "create subtab") | ❌ |
| **Reorder fields (drag)** | ✓ | ❌ (verified: no "reorder") | ❌ |
| **Column break / same-as-previous** | ✓ | ❌ | ❌ |
| **Pick sublist for a subtab** | ✓ | ❌ (needs line fields first) | ❌ |
| Show/hide/reorder line fields | ✓ | ❌ | ❌ |

**Reality:** the screen is a **form registry** (list forms, create/copy, assign roles), not a
**layout designer**. NetSuite's power is the drag-and-drop layout editor. This is the single
biggest build in your document.

## 5. SUBTABS & SUBLISTS AS OBJECTS — ❌ MISSING

Subtabs today are string labels on placed fields, not managed objects. Sublists don't exist.
Both are NetSuite metadata objects. **New build, coupled to §2 and §4** — treat line-fields +
subtab-objects + sublist-objects as ONE slice; splitting them will thrash.

## 6. GENERAL LIFECYCLE (create/view/edit/inactivate/delete) — ⚠️ INCONSISTENT

Your remark "some cases I can't edit or delete." Verified matrix:

| Element | Create | Edit-self | Inactivate | Delete | Verdict |
|---|---|---|---|---|---|
| Custom field def | ✓ | ✓ | ✓ | ✓ (if unused) | ✅ |
| Custom list (self) | ✓ | ❌ | ❌ | ❌ | ❌ |
| Custom list value | ✓ | ✓ | via active | ✓ (guarded) | ✅ |
| Segment def | ✓ | ✓ | active flag | ❌ no delete | ⚠️ |
| Segment value | ✓ | ❌ no PUT | ? | ❌ | ❌ |
| Entry form | ✓ | ✓ | ? | ✓ | ⚠️ |

**NetSuite gives every custom element the same lifecycle verbs.** eProcure is a patchwork.
**Standardize:** every element gets create/view/edit/inactivate/delete with dependency guards,
uniform across the board.

## 7. ADMINISTRATION IA — ✅ MOSTLY DONE

All elements already sit under the **Administration** center-tab (User Mgmt, Custom Lists,
Custom Fields, Segments, Entry Forms, Numbering). Your ask is met unless you want a nested
"Customization" sub-header — a small grouping change. **Confirm intent.**

## 8. ICONS — ❌ SHARED, measurably

Verified nav icon reuse: `clip` ×5, `edit` ×3, `doc` ×3, `box` ×3. Custom Lists & Onboarding
share `clip`; Custom Fields, Entry Forms, Numbering draw from `edit`/`clip`. `Icon.tsx` is a
clean single-source SVG map, so **adding unique glyphs is easy**. Give each Administration
element (and the new customization objects) its own distinct glyph.

## 9. SIDEBAR COLLAPSE — ❌ MISSING

Verified: **0 collapse controls.** Self-contained UI feature (toggle + width state + slim rail).

## 10. SAVED VIEW → SAVED SEARCH — ⚠️ engine real, universe narrow

**NetSuite saved search:** pick record type (any) → criteria (operators, AND/OR, nested) →
results (columns, sort, summary/grouping) → available as list/KPI/reminder/portlet.

| Function | NetSuite | eProcure today (verified) | Verdict |
|---|---|---|---|
| Pick record type | any record | 7 only (verified in reminder modal dropdown): Requisition, Rfq, PO, Invoice, Asn, Vendor, Onboarding | ⚠️ narrow, **no User/Item** |
| Entities as a class | Users, Vendors, Items | only Vendor | ⚠️ (User/Item deferred per your note) |
| Transactions as a class | ✓ | ✓ (all present) | ✅ |
| **Segments as a searchable class** | ✓ | ❌ (segments are dimensions, not a search target) | ❌ |
| Criteria (operators/AND-OR) | rich | basic filters | ⚠️ needs richer operators |
| Result columns + sort | ✓ | ✓ | ✅ |
| Summary/grouping | ✓ | partial (KPI groupBy) | ⚠️ |
| Feed KPI/reminder/portlet | ✓ | ✓ (model + modal) but **picker near-empty** (1 seeded view) | ⚠️ |
| Create-view flow discoverability | ✓ | ⚠️ views exist but making one to feed a reminder isn't obvious | ⚠️ |

**Per your note:** User/Item as entities are **deferred** — fine. So the SavedView work is:
(a) richer criteria/operators, (b) segments-as-a-searchable-class, (c) make the create→feed
loop discoverable, (d) seed more example views so the pickers aren't empty. **Drop
"customizations as a search class"** (you doubted it; NetSuite doesn't saved-search field
defs either — use a normal admin list view).

## 11. DASHBOARD & PORTLETS — ⚠️ model ready, UI absent for the 3 things you named

**NetSuite:** personalize dropdown "bucket" of addable portlets · drag-drop rearrange ·
remove/re-add · reminders/KPIs bound to saved searches · tile shortcuts with colour + target.

| Function | NetSuite | eProcure today (verified in browser) | Verdict |
|---|---|---|---|
| Personalize (make own copy) | ✓ | ✓ (button works, creates personalized copy) | ✅ |
| **Add-portlet bucket/dropdown** | ✓ | ⚠️ only "Add KPI" + "Add reminder" buttons — not the full type bucket | ⚠️ |
| **Drag-drop rearrange** | ✓ | ❌ (verified: 0 draggable) — Row/Col exist in model, no UI | ❌ |
| **Remove portlet** | ✓ | ❌ (verified: 0 remove buttons) | ❌ |
| KPI bound to metric | ✓ | ✓ | ✅ |
| **KPI/reminder bound to saved view** | ✓ | ⚠️ model+modal exist; picker shows ~1 view so reads empty | ⚠️ |
| Reminders portlet | ✓ | ✓ exists; ⚠️ add-reminder modal works but few views to pick | ⚠️ |
| **Shortcuts/tiles: add-new** | ✓ | ❌ (verified: 0 add-shortcut) | ❌ |
| **Tiles: choose colour** | ✓ | ❌ | ❌ |
| **Tiles: choose target page** | ✓ | ❌ (ShortcutItem has Route but no authoring UI) | ❌ |

**Reality:** the portlet *model* is rich (8 types, Row/Col/Width, SavedViewId, typed configs),
but the **interaction layer is missing** for drag, remove, tile-authoring, and the add-bucket.
This is a **front-end slice against a ready model** — smaller than it feels, high user impact.

## 12. GLOBAL SEARCH NAVIGATION — ❌ misroutes 3 record types

**Verified:** searched `PR-2026-0412`, clicked it, landed on `#reqs` (the list), not the PR.
Root cause in `hitRoute` (`TopBarNav.tsx`): `case 'Requisition': return 'reqs'` with an honest
`// no deep link today` comment. `Statement` and `Asn` fall to `default: return null` — **dead
clicks**. PO/Invoice/RFQ/Vendor deep-link correctly.

| Record type | Deep-links to detail? |
|---|---|
| Vendor, RFQ, PO, Invoice | ✅ yes |
| **Requisition** | ❌ goes to list |
| **ASN (delivery)** | ❌ dead click |
| **Statement** | ❌ dead click |

**Fix:** give Requisition/ASN/Statement real detail routes + point `hitRoute` at them.

---

# THE BUILD PLAN

Your instruction: get the framework clean and NetSuite-standard before the transaction
features, so the pattern standardizes. Sequenced by dependency and value:

## Slice CF1 — Quick parity wins (independent, high-visibility)
*The band that makes the framework feel real. All independent, low-risk.*
1. **Money display format** — `Intl.NumberFormat` on Money/Decimal in `renderField`
   (`100,000.00`). Storage already correct.
2. **Custom-list edit** — add `PUT /custom-lists/{code}` (rename/description) + **order-mode**
   flag (entered/alphabetical) + inactivate/delete-list. Closes §3 gaps.
3. **Icon uniqueness** — distinct glyph per Administration element (and future customization
   objects). Closes §8.
4. **Sidebar collapse** — toggle + width state + slim rail. Closes §9.
5. **Global-search routing** — Requisition/ASN/Statement detail routes + `hitRoute`. Closes §12.

## Slice CF2 — Uniform lifecycle
*Make every custom element edit/inactivate/delete the same way, with dependency guards.*
6. **Segment value edit** (add PUT), **segment def delete** (guarded), and audit every element
   for the full create/view/edit/inactivate/delete verb set. Closes §6.

## Slice CF3 — Dashboard flexibility (front-end against a ready model)
*Biggest felt win for least architectural risk.*
7. **Drag-drop rearrange** — wire DnD to existing Row/Col/Width; persist via `PUT /mine`.
8. **Remove portlet** — per-portlet remove control; persist.
9. **Add-portlet bucket** — a "Personalize" dropdown listing all 8 portlet types (not just KPI
   + reminder); add instance.
10. **Tile/shortcut authoring** — add-new, choose colour, choose target page (Route already in
    model). 
11. **Populate the pickers** — seed more example saved views per role AND make the
    create-view→feed-reminder/KPI loop discoverable, so the reminder/KPI view-pickers aren't
    near-empty. (This is what made you feel reminders "don't work.")

## Slice CF4 — Custom-field authoring parity
12. **Display type** (normal/disabled/inline) on the field, **insert-before** picker,
    **show-in-list** flag. Optional: multiselect/datetime types, multi-record applies-to.
    Explicitly **drop** global-search/encrypted (low value). Closes §1 gaps.

## Slice CF5 — Entry-form LAYOUT EDITOR (the big one)
13. Turn the form *list* into a **layout designer**: create/rename/reorder/**subtabs as
    objects**, **create field groups**, per-field **show/hide/reorder (drag)**, **column
    break**. Closes §4 (body-field portion) and §5 (subtab objects).

## Slice CF6 — Custom LINE fields + sublists (new architecture)
14. **Line-scoped custom fields** (`LineId` on values, line placement) + **sublist objects** +
    sublist selection in a subtab + line-field show/hide/reorder. Closes §2, and the sublist
    portion of §4/§5. *One coupled slice — do not split.*

## Slice CF7 — Saved View → Saved Search generalization
15. **Richer criteria** (operators, AND/OR), **segments as a searchable class**, summary/
    grouping parity, and (deferred per your note) the entity classes User/Item when those
    entities exist. **Drop** "customizations as a search class." Closes §10.

---

## Recommended order & why
CF1 → CF2 → CF3 → CF4 → CF5 → CF6 → CF7.
- CF1–CF3 are mostly **surfacing/wiring** — fast, visible, low-risk; they make the framework
  *feel* NetSuite-grade within days.
- CF4 is bounded authoring polish.
- CF5 and CF6 are the **real new architecture** (layout editor; line fields/sublists) and
  deserve their own Step 0 file plans and the most care.
- CF7 is the deepest data work but sits cleanly last.

You said build everything and verify line-by-line. The accompanying **PROMPT-CF-AUDIT-BUILD**
does exactly that in one pass: check-what's-built, build-what's-missing per this plan, then
test every function and compare line-by-line against **both** this document and your original
`Build_Comments.docx` before handing back for your review.

**Scope gate (so this actually closes):** for each NetSuite affordance, the test is *does an
SPSB procurement user touch it in a sourcing workflow?* Show-in-list: yes. Column break:
marginal (build it, it's cheap). Global-search-on-custom-field / encrypted fields: no — dropped.
Your instincts flagged the same three doubts; they were right each time.
