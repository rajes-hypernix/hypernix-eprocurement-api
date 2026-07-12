# AUTO-DECISIONS — LONGRUN Phase 3 (calls made for you; veto any and its slice reverts)

Everything of consequence was pre-decided in CF5-CF7-LOCKED-DECISIONS.md and followed
verbatim. The entries here are the implementation-level calls that file did not cover.

## AD-1 (CF5): a field carries GroupId ONLY — its subtab derives via the group
- **Decision:** EntryFormField FKs its group; the group FKs its subtab (or body). The
  Step 0 sketch mentioned both SubtabId and GroupId on the field.
- **Why:** a field-level SubtabId would duplicate the group's — an invariant
  (field.SubtabId == group.SubtabId) that can silently drift. One FK, no police.
- **Blast radius:** entry-form storage + EntryFormService only; wire DTOs unchanged.
- **Reverse:** add the redundant SubtabId column + backfill from groups (one migration).
- **Commits:** 4650bae, 656821f, 07ede4b.

## AD-2 (CF5): container SYNC-PRESERVE on field saves
- **Decision:** saving the composer's field list creates missing subtabs/groups by name
  and never deletes containers; deletion happens only via the guarded endpoints.
- **Why:** wholesale replace would nuke Hidden/ColumnBreak state and explicitly-created
  empty subtabs on every save.
- **Blast radius:** EntryFormService.UpdateAsync semantics; orphaned EMPTY groups can
  accumulate if a user renames placements — they surface in the panel with a delete ✕.
- **Reverse:** switch UpdateAsync back to wholesale replace (one method).
- **Commits:** 656821f.

## AD-3 (CF6): partial-index uniqueness — PRE-DECIDED by the locked-decisions file
- Recorded here per its own instruction: CF6-T1 uses two partial unique indexes
  (WHERE LineId IS NULL / IS NOT NULL) instead of NULLS NOT DISTINCT, for prod-PG-version
  safety. **Reverse:** swap to a single NULLS NOT DISTINCT index IF prod is confirmed
  PG15+ and the operator prefers it (one migration). **Operator: please confirm the
  Azure Malaysia West Postgres version** (also flagged in BLOCKERS).
