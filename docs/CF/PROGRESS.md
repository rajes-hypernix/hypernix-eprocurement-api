# CF autonomous run — timestamped progress log
[19:17 UTC] PHASE A done: baselines API 462 / web 232 / crawl 53; audit written (2 PLAN corrections: T1 mostly built, T8 built behind Arrange); ledger Audit column filled 37/37
[19:20 UTC] CF1-T1: money pin (already-built, now proven) — see git log — CF1-T1 spec
[19:28 UTC] CF1-T2: list self-lifecycle + order-mode — see git log — CF1-T2 spec
[19:29 UTC] CF1-T3: distinct admin icons + reserved glyphs — see git log — CF1-T3 spec
[19:31 UTC] CF1-T4: sidebar collapse — see git log — CF1-T4 spec
[19:35 UTC] CF1-T5: search deep-links + ASN/Statement hit classes — see git log — CF1-T5 spec
[19:36 UTC] CF1-T5b: red-gate repair (process slip logged) — 465/465
[19:42 UTC] CF2-T6 + slice gate: uniform lifecycle — see git log — CF2-T6 spec
2026-07-13 04:05 CF3-T10 772f588 — authorable shortcut tiles: colour palette (data, closed set), target picker, Add-tile modal; ShortcutItem.Color server-side. Test CF3-T10.
2026-07-13 04:05 CF3-T9 4860aec — Add-portlet bucket: all 7 types from one dropdown, per-type minimal config; KpiMeter/Reminders route to richer modals. Test CF3-T9.
2026-07-13 04:05 CF3-T11 3d63973 — 5 idempotent VIEW-DEMO-* shared example views (fixes 'reminders don't work': Requisition picker had 0); empty picker → 'create one in Saved Views' CTA. Test CF3-T11 (incl. on-screen create-view→KPI loop; Onboarding empty-picker CTA leg).
2026-07-13 04:05 CF3-T7 c759bc3 — drag-drop swap in arrange mode (HTML5 DnD on Row/Col model, saved by Done) + dashboard hub wiring + browser tests CF3-T7..T11. CF3-T8 remove was pre-existing (audit PLAN correction) — proof CF3-T8 incl. Reset-restores.
2026-07-13 04:05 CF3 SLICE GATE — PLAN §11 re-read: 10/10 table rows covered, 13/13 boxes ticked, 0 BLOCKERS. Note: Dashboard.tsx is a hub file, so T7's commit carries the shared wiring for T9/T10/T11; every commit tree is import-complete and gates ran green on the full tree before the sequence.
2026-07-13 04:19 CF4-T12 920fb6c — def-level DisplayType (server-enforced display-only for Disabled/Inline) + insert-before (sibling-order normalization) + show-in-list (system-view runs only). Migration CustomFieldAuthoring (planned, PLAN §1/CF4). Dropped: global-search/encrypted (PLAN ruling). Deferred with reasons: multiselect (typed-value CHECK design), DateTime (standing D5 ruling). Tests CF4-T12 + 3 xUnit (467→470).
2026-07-13 04:19 CF4 SLICE GATE — PLAN §1 re-read: all three ❌ rows built+proven, drops/deferrals recorded, 6/6 boxes, 0 BLOCKERS.
2026-07-13 04:36 BAND REPAIR — sidebar nav aria-label now collapsed-only; 4 pre-CF specs (05/06/07/09) green again; full e2e 65/65.
2026-07-13 04:36 AUTONOMOUS BAND COMPLETE — 39/39 CF1–CF4 boxes ticked, BLOCKERS empty; dotnet 470 · vitest 233 · e2e 65/65 (all raised vs A0); results.json regenerated; tagging v1.0-cf-autonomous-band.
2026-07-13 04:39 CF5/CF6/CF7 STEP 0 — three plans written (layout editor, line fields+sublists, saved search); every migration named; zero implementation code, per the band boundary.
2026-07-13 04:40 PHASE C — CF-VERIFICATION.md written (37 PLAN rows + 19 Build_Comments paragraphs, all accounted); BLOCKERS.md empty; morning report posted. CF programme handoff complete.
2026-07-13 06:45 LONGRUN P1 baselines — dotnet 471 · vitest 233 · e2e 65 (after SWEEP-FIX-T1 e6cf324: entry-form copy de-dupe + UI error surfacing; found by the baseline run).
2026-07-13 06:45 LONGRUN P1 TEST-SWEEP-T1/T2 edcbc88 — 8 new tests close every inventory gap (dev-404, one-award, 8-types, line-grain segments, FK floor, CHECK 23514, xmin×9, inactive-value options).
2026-07-13 06:45 LONGRUN P1 COMPLETE — 69/69 inventory boxes evidence-filled; B2/B3 blocked-with-reason; FULL-TEST-REPORT.md written; suites 478/234/65; tagging v1.1-full-test-sweep.
2026-07-13 06:48 LONGRUN P2 COMPLETE — 12/12 dimensions probed: verdict CLEAN-WITH-NOTES (2 notes: dev-era migration drops → recommend pre-prod squash; e2e fixed sleeps). 0 fixes needed beyond the already-committed warning. Tagging v1.2-backend-clean.
2026-07-13 06:56 CF5-T1 — EntryFormLayout migration: subtabs+groups as OBJECTS, fields FK groups (subtab derives via group), md5-deterministic backfill matching EntryFormSeed HexGuid. PARITY STOP GATE PASSED: resolve byte-identical for u_faridah/u_lim/u_admin before vs after. Wire contract unchanged (strings resolve from objects). Gates 478/234/08+CF-parity green.
2026-07-13 07:13 CF5-T2+T3 656821f — layout-object CRUD (subtab hide warn-but-allow per locked D2; guards 409 on populated containers); resolve excludes hidden subtabs; column break exposed.
2026-07-13 07:13 CF5-T4+T5 07ede4b — composer panel (chips, drag placement, warnings) + two-column canvas (locked D3); guardrails caught raw inputs → rebuilt on FieldSpec primitives. Proofs CF5-T2..T5.
2026-07-13 07:13 CF5 SLICE GATE — Step 0 T1–T5 all shipped+proven; parity STOP gate passed (byte-identical ×3 roles); full e2e 69/69; AD-1/AD-2 recorded. Tagging v1.3-cf5.
2026-07-13 07:37 CF6-T1 c89afcd — line-scope model landed with locked partial-index uniqueness; STOP-gate held (480/480, CHECKs untouched).
2026-07-13 07:37 CF6-T3 — PR lines table renders Line-scope custom columns (editable, saved with the form, ownership-verified); admin Scope authoring; browser journey CF6 green (author→enter→persist at line grain, no header bleed).
2026-07-13 07:37 CF6 partial-close — sublist objects deferred to BLOCKERS B4 (complete CF7 chosen over a half-built sublist UI); tagging v1.4-cf6-line-fields.
2026-07-13 07:55 CF7-T1+T2 643bb7d — 8 operators (null semantics pinned FIRST, per lock) + grouped-OR (ViewFilterGroups, default 0 = legacy); builder 'or…'; browser proof green. NO ViewDefaultGrouping migration (locked). T3/T4 deferred → BLOCKERS B5.
2026-07-13 07:55 LONGRUN CLOSE — suites dotnet 482 · vitest 234 · e2e 71/71; tags v1.1..v1.5; morning report posted.
2026-07-13 12:39 CF-FIX-1 CAMPAIGN — T1..T10 shipped (39066ee..796050f + 4f1b84d): labels/full names, insert-before removed (CF4 reversal annotated), order-mode at create, list+field Internal IDs, auto-numbered value ids, searchable select (keyboard gate), 15 types w/ live-DB insert gate + migration CustomFieldTypeExpansion, depends-on hardened, value trees. Bonus repair: lines-only saves no longer blocked by unmet required header fields. Suites 504/237/81 (from 482/234/71). Tag v1.6-cf-fix-1.
2026-07-13 16:34 CF-FIX-2 CAMPAIGN — T4 square corners (8021431), T1 contextual prefixes+affix (8ab78c2), T2 searchable-everywhere + Active-default BUG REPAIR (9ad6887), T5 staged values (c530c80), T3 SHARED FIELDS + PR→PO carry with audit skip-notes (47dea70). Migration SharedCustomFields (join table, grandfathered). Suites 506/237/86 (from 504/237/81). Tag v1.7-cf-fix-2.
