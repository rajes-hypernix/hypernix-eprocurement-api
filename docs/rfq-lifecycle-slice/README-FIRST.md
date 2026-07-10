# README-FIRST — RFQ Lifecycle Extension (Slices I & J)

Read this before anything else. These constraints are NON-NEGOTIABLE and apply
to both slices in this package.

1. **Additive only.** Existing flows — Confirm Lines, Consolidate, RFQ Builder,
   Bid Openings, Technical Evaluation, Award — must behave exactly as before.
   The only permitted change to existing behaviour is replacing the internal
   *representation* of invited vendors (delimited string → RfqInvitation rows);
   every screen and endpoint that consumed the old representation must produce
   identical observable results afterwards.

2. **Invitation state is NOT RFQ state.** The Rfq.Status enum
   (Draft/Open/Closed/UnderEvaluation/Awarded/Cancelled) gains NO new values.
   Decline, rescind, viewed, intend-to-bid live on RfqInvitation only.

3. **Domain methods enforce every transition.** No `.Status =` assignments from
   services. Illegal transitions throw `DomainRuleException`. Status setters are
   private.

4. **Append-only facts.** RfqEvent rows and RfqInvitation rows are never
   deleted and (except for status transitions via domain methods) never
   updated. Rescind is a transition, not a delete.

5. **Analytics-first schema.** Typed UTC timestamp columns, controlled reason
   codes (no free-text-only reasons), derived counts (extension count is
   COUNT of events, never a stored integer), no JSON where a typed column
   will do.

6. **IClock for all timestamps.** Zero `DateTime.UtcNow` outside IClock.

7. **One reversible migration per slice.** Slice I's migration includes the
   invitation backfill; its `Down()` must reconstruct the delimited column.

8. **Probity window is sacred.** Between RFQ close and envelope opening,
   NOTHING about invitations or deadlines may change. Every guard in the spec
   that references this window must be enforced in the domain, not the UI.

9. **No auth implementation.** New actions are recorded in
   PERMISSIONS-REGISTER.md (created by Slice I) for the deferred auth pass.
   Do not add [Authorize] attributes in these slices.

10. **Report before touching.** In each slice, list every file you intend to
    create or modify, grouped by task, and WAIT for confirmation before
    changing anything.
