# STEP 0 — CF1 Quick parity wins (autonomous; self-verified)

All CF browser tests land in ONE growing spec: `e2e-audit/tests/10-cf-parity.spec.ts`
(one `test()` per ledger Test-column entry, named `CF1-Tn-*`).

## T1 — Money display (audit: mostly EXISTS — pin + residual)
- `e2e-audit/tests/10-cf-parity.spec.ts` — CF1-T1: PO read surface shows grouped `RM x,xxx.xx`;
  a Money custom field displays grouped on blur and submits the RAW numeric (round-trip pinned
  via API readback). No production code change expected (audit); if the pin exposes a raw spot,
  fix it inside T1.

## T2 — Custom-list edit (the ledger's ONE named migration: `CustomListOrderMode`)
- Domain `CustomList`: + `OrderMode` (string: Entered|Alphabetical, default Entered) + `Active`
  (bool, default true) — one migration `CustomListOrderMode` carries both (both are T2 columns;
  the inactivate verb needs Active).
- API: `PUT /custom-lists/{code}` (name/description/orderMode — A31 ManageCustomLists),
  `POST /custom-lists/{code}/active` (toggle), `DELETE /custom-lists/{code}` (guarded: any
  cf-def binding or in-use value → 409/deactivate path; unreferenced+user list → hard delete;
  IsSystem lists never deletable).
- Option resolvers apply OrderMode: web `lib/lookups.ts` (`of()` sorts by label when
  Alphabetical) + server `SegmentService`/`EntryFormService` option builders if they sort.
- Web: AdminCustomLists edit-list modal (rename/desc/order-mode), deactivate/delete controls;
  client fns.
- Tests: API guard tests + browser CF1-T2 (rename a list, flip to Alphabetical, options re-order
  in a picker; delete blocked on referenced list).

## T3 — Icon uniqueness
- `Icon.tsx`: new glyphs `list` (custom lists), `field` (custom fields), `form` (entry forms),
  `hash` (numbering), `users2`/keep `vendor` distinct, + RESERVED `subtab`, `sublist` for CF5/CF6.
- `centerTabs.ts`: every Administration item gets a distinct glyph; sweep other groups so no
  ADMIN item shares with anything.
- vitest: extend nav-coverage — Administration icons pairwise distinct. Browser: CF1-T3 asserts
  the rendered admin nav has no duplicate icon names (data-icon attr on Icon svg for the probe).

## T4 — Sidebar collapse
- `Sidebar.tsx`: collapse toggle button, slim icon-only rail (CSS class), localStorage-persisted
  per principal; main widens (shell CSS grid).
- Browser CF1-T4: toggle → rail narrow + labels hidden; reload → still collapsed; toggle back.

## T5 — Global-search routing
- AUDIT DELTA: server emits NO Asn/Statement hits at all — the "dead click" is a missing hit
  class, not just a null route. Add Asn + Statement to `SearchService` behind the OD-4 type
  filter (Asn→ViewAsns, Statement→ViewStatements(internal)/ViewMyStatement handling: internal
  statements are per-vendor → hit id = vendorId).
- `TopBarNav.hitRoute`: Requisition → `reqs/open/{id}` (new deep-link param on Requisitions,
  same pattern as `reqs/view/`), Asn → `deliveries/asn/{id}` (exists), Statement →
  `statements/{vendorId}` (exists).
- `Requisitions.tsx`: `initialOpenId` prop → opens PrForm directly; App routes `reqs/open/{id}`.
- Browser CF1-T5: search a PR code → click → PrForm detail; ASN → ASN detail; statement →
  statement detail; regression: PO/Invoice/RFQ/Vendor unchanged.
