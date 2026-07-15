-- CFH-T2: hard-delete the litter config (dev DB). Custom FIELDS + SEGMENTS were deleted
-- through the governed API (now value-free after T1, so DELETE not deactivate): 84 applied
-- + 34 orphan litter field defs (Warranty Expiry, CF4 Star, Fix3/4/5, Budget Cap/Ref, Site
-- Ref, Probe, T2 OnCustom, Test Label Mandatory, Placed) and 11 litter segments (Project
-- nnnnnn, Fix4 Seg, fgv, and the operator-named "Project Code Grouped"). The SQL below covers
-- the parts the API could not do: shared/unowned VIEW-DEMO views, the referenced Partner List,
-- and dangling FieldRegistry rows left by earlier sessions.
BEGIN;
-- Litter shared example views (VIEW-DEMO-*), children first (FK Restrict). Keep VIEW-SYS-*.
DELETE FROM "SavedViewColumns" WHERE "SavedViewId" IN (SELECT "Id" FROM "SavedViews" WHERE "Code" LIKE 'VIEW-DEMO-%');
DELETE FROM "SavedViewFilters" WHERE "SavedViewId" IN (SELECT "Id" FROM "SavedViews" WHERE "Code" LIKE 'VIEW-DEMO-%');
DELETE FROM "SavedViews" WHERE "Code" LIKE 'VIEW-DEMO-%';
-- Test-only custom lists (Partner List / Customer) — remove their values then the list.
DELETE FROM "CustomListValues" WHERE "CustomListId" IN (SELECT "Id" FROM "CustomLists" WHERE "Code" IN ('CUSTLIST_PARTNER','_CUSTOMER'));
DELETE FROM "CustomLists" WHERE "Code" IN ('CUSTLIST_PARTNER','_CUSTOMER');
-- Dangling FieldRegistry rows for deleted custom fields (would pollute the view-builder palette).
DELETE FROM "FieldRegistry" WHERE "Kind"='Custom' AND "FieldKey" NOT IN (SELECT "Code" FROM "CustomFieldDefs");
COMMIT;
