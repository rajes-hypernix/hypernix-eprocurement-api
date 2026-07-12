# CF PHASE A — AUDIT FINDINGS (2026-07-13, autonomous run)

**A0 baselines (live):** API **462** · web **232** · crawl **53** — all green. Stack: PG + API :5260 + Vite :5173.

**Method:** code inspection + live API + browser probes (playwright, personas). Where a finding
contradicts the PLAN verdict it is marked **CORRECTION** and build scope adjusts.

| Ledger item | Audit finding | Scope effect |
|---|---|---|
| CF1-T1 money grouped | **PARTIAL-ALREADY**: `fmt()` (en-MY, 2dp, grouped) already used on read surfaces; `MoneyField` already formats grouped ON BLUR and reverts to raw on focus (round-trip works by construction). Probe: read cell "RM 100.00". Residual: `NumberField` (Decimal/Int cf fields) does NOT group; no browser test pins any of it | verify + close Decimal residual + pin with browser test |
| CF1-T1 round-trip | already works (display-on-blur only; raw value preserved) — untested | pin with browser test |
| CF1-T2 PUT /custom-lists | confirmed ❌ — controller has GET/POST + value verbs only | build |
| CF1-T2 order-mode | confirmed ❌ — no OrderMode on CustomList; migration `CustomListOrderMode` named by ledger | build |
| CF1-T2 inactivate/delete list | confirmed ❌ — no endpoint; `Active` exists on VALUES only | build |
| CF1-T2 admin UI | confirmed ❌ — 0 edit-list controls in browser | build |
| CF1-T3 icons | confirmed ❌ — clip×5, edit×3, doc×3, box×3 in centerTabs | build |
| CF1-T4 collapse | confirmed ❌ — 0 collapse controls in Sidebar.tsx | build |
| CF1-T5 Requisition hit | confirmed ❌ — probe: click PR hit → `#reqs` (list). `hitRoute` returns 'reqs' with honest comment | build (PR detail route EXISTS: `reqs/{id}` opens PrForm? NO — Requisitions is state-driven; deep-link needs a route param like `reqs/open/{id}` or reuse `reqs/view/`… detail below) |
| CF1-T5 Asn/Statement hits | confirmed ❌ — `default: return null` = dead clicks | build |
| CF1-T5 regression 4 types | confirmed ✅ working (Vendor/Rfq/PO/Invoice deep-link) | pin in test |
| CF2-T6 segment value PUT | confirmed ❌ — SegmentsController has POST values only | build |
| CF2-T6 segment def DELETE | confirmed ❌ — no DELETE /{id} | build |
| CF2-T6 lifecycle audit | matrix verified: list self-verbs ❌ (CF1-T2 builds); segment def delete ❌ + value edit/inactivate ❌; entry form has create/edit/delete/roles but NO inactivate toggle (Active field exists, never settable) | build gaps |
| CF2-T6 guards | existing guards confirmed (cf def valued→deactivate-only; segment unapply 409; list value in-use→deactivate A2F-T3); new verbs must match | build with guards |
| CF3-T7 drag-drop | confirmed ❌ — 0 draggable, even in arrange mode (arrows only) | build |
| CF3-T8 remove | **CORRECTION**: remove EXISTS — Personalize → Arrange shows 9 per-portlet remove buttons, persisted via PUT /mine (D4). PLAN's "0 remove buttons" was measured on a NON-personalized dashboard | verify + pin with browser test (no rebuild) |
| CF3-T9 add-bucket | confirmed ⚠️ — only "Add KPI" + "Add reminder"; no bucket for Shortcuts/RecentRecords/Chart/SavedViewList/KpiScorecard | build |
| CF3-T10 tile authoring | confirmed ❌ — 0 add-shortcut controls; ShortcutItem has Route, no colour member (colour = ADDITION to the sanctioned config shape) | build |
| CF3-T11 picker density | confirmed — Requisition picker: **0 views** (no system view per D7.5 ruling, none seeded); Rfq 1, PO 2, Invoice 1. The operator's "reminders don't work" reproduced exactly | build (seed + discoverability) |
| CF4-T12 display type | confirmed ❌ — no DisplayType on CustomFieldDef (form placement has it; the def does not) | build |
| CF4-T12 insert-before | confirmed ❌ — integer sort only | build |
| CF4-T12 show-in-list | confirmed ❌ — no flag; registry rows exist for cf fields (Kind=Custom) so saved-view columns CAN already carry them — the flag drives default list columns | build |
| CF4-T12 dropped items | global-search + encrypted: confirmed absent, DROPPED per ruling | record only |
| CF4-T12 multiselect/datetime | absent; "optional if cheap" | assess at CF4 |

**Plan-contradiction summary (scope adjustments):**
1. **T1 is mostly built** — money grouping exists on read + edit surfaces; work = Decimal residual + browser pins.
2. **T8 is built** — work = browser pin (and CF3-T7's DnD will naturally sit beside it).
3. Everything else: PLAN verdicts confirmed by probe.
