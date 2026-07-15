-- CFH-T5 final sweep: remove TEST-CREATED config litter that accumulated in the dev DB from
-- prior e2e runs (these are NOT produced by the seeder, so once gone they stay gone; a truly
-- fresh DB never had them). Children first (FK Restrict), then the parent def/segment.
BEGIN;

-- Litter custom fields: any stamped def whose LABEL ends in a 5-6 digit test stamp (e.g.
-- "Site Ref 204174", "Warranty Expiry 993964") and which holds ZERO values. The curated
-- fields (Partner, Remarks) never end in digits, so they are untouched. Children first.
DELETE FROM "CustomFieldDefApplications" WHERE "FieldDefId" IN (
    SELECT d."Id" FROM "CustomFieldDefs" d WHERE d."Label" ~ '[0-9]{5,6}$'
      AND NOT EXISTS (SELECT 1 FROM "CustomFieldValues" v WHERE v."FieldDefId" = d."Id"));
DELETE FROM "FieldRegistry"           WHERE "CustomFieldDefId" IN (
    SELECT d."Id" FROM "CustomFieldDefs" d WHERE d."Label" ~ '[0-9]{5,6}$'
      AND NOT EXISTS (SELECT 1 FROM "CustomFieldValues" v WHERE v."FieldDefId" = d."Id"));
DELETE FROM "CustomFieldDefs" d WHERE d."Label" ~ '[0-9]{5,6}$'
      AND NOT EXISTS (SELECT 1 FROM "CustomFieldValues" v WHERE v."FieldDefId" = d."Id");

-- Litter segments: test-created "Fix4 Seg nnnnnn" and "Project nnnnnn" (NOT the curated "Project").
DELETE FROM "SegmentAssignments" WHERE "SegmentValueId" IN (
    SELECT v."Id" FROM "SegmentValues" v JOIN "SegmentDefs" d ON d."Id" = v."SegmentDefId"
    WHERE d."Name" ~ '^(Fix4 Seg|Project) [0-9]{5,6}$');
DELETE FROM "SegmentValues"      WHERE "SegmentDefId" IN (SELECT "Id" FROM "SegmentDefs" WHERE "Name" ~ '^(Fix4 Seg|Project) [0-9]{5,6}$');
DELETE FROM "SegmentApplications" WHERE "SegmentDefId" IN (SELECT "Id" FROM "SegmentDefs" WHERE "Name" ~ '^(Fix4 Seg|Project) [0-9]{5,6}$');
DELETE FROM "FieldRegistry"      WHERE "SegmentDefId" IN (SELECT "Id" FROM "SegmentDefs" WHERE "Name" ~ '^(Fix4 Seg|Project) [0-9]{5,6}$');
DELETE FROM "SegmentDefs"        WHERE "Name" ~ '^(Fix4 Seg|Project) [0-9]{5,6}$';

-- Litter custom lists: test-created run-stamped lists (e.g. "CF List 123456", "Fix3 List 123456")
-- whose name ends in a 5-6 digit stamp. The curated procurement lists have plain names. Values
-- first (FK), then the list. (A def still bound to one would block on the def's FK — but the field
-- sweep above already removed the stamped test fields that bound them.)
DELETE FROM "CustomListValues" WHERE "CustomListId" IN (SELECT "Id" FROM "CustomLists" WHERE "Name" ~ '[0-9]{5,6}$');
DELETE FROM "CustomLists"      WHERE "Name" ~ '[0-9]{5,6}$';

-- Litter saved views: anything that is NOT a System view and NOT one of the 5 curated VIEW-DEMO
-- examples (AD-5) is test-created run litter. Columns + filters first (FK Restrict), then the view.
DELETE FROM "SavedViewColumns" WHERE "SavedViewId" IN (SELECT "Id" FROM "SavedViews" WHERE "IsSystem" = false AND "Code" NOT LIKE 'VIEW-DEMO%');
DELETE FROM "SavedViewFilters" WHERE "SavedViewId" IN (SELECT "Id" FROM "SavedViews" WHERE "IsSystem" = false AND "Code" NOT LIKE 'VIEW-DEMO%');
DELETE FROM "SavedViews"       WHERE "IsSystem" = false AND "Code" NOT LIKE 'VIEW-DEMO%';

COMMIT;
