# README-FIRST — Slice RM: Role-Matrix Authorization

The last outstanding authorization item on PERMISSIONS-REGISTER.md, sequenced
before D3 because the saved-view executor and <Gated> both need real server
permissions behind them. Constraints are non-negotiable.

1. **Server catalog is the single source of truth.** One declarative
   action catalog (action → allowed roles) in the API. The web's gating map
   is DERIVED from it at runtime, not maintained in parallel. After this
   slice, ui/gating.ts's static map is dead.

2. **Deny is 403, and only after 401.** Authentication (Slice F) stays
   untouched: no principal → 401 via the fallback policy, exactly as today.
   Authenticated-but-wrong-role → 403 with the existing problem-details
   shape. The anonymous-sweep test must pass UNCHANGED.

3. **Behaviour-preserving for legitimate users.** Every persona doing its
   own job sees zero change — the crawl (42/42, role-aware personas) is the
   proof. What changes is that cross-role calls the UI never makes now fail
   at the server instead of succeeding silently.

4. **The matrix is operator-reviewed before enforcement.** Step 0 produces
   the complete endpoint × action × role table; NOTHING is enforced until
   the operator has ruled on it. Ambiguous rows are questions, not guesses.

5. **Vendor resource-scoping (Slice F) is not this slice.** It stays as-is
   and its tests stay green. This slice separates INTERNAL roles (Buyer,
   Approver, TechEvaluator, CommEvaluator, Admin) and pins the Vendor
   principal's action surface.

6. **Enforcement must be drift-proof.** A new endpoint added without an
   action assignment must FAIL a test by default — the sweep-test
   philosophy applied to authorization.

7. Commit per phase, CI green per push, five-lens review in Step 0 and the
   final report. Baselines at start: API 304, web 198, crawl 42/42 —
   record exact, hold or raise.

## Out of scope
DoA threshold configurability or any workflow engine (L5); record-level
permissions beyond existing vendor scoping; role-assignment UI changes
(AdminUsers exists); new roles; the D3+ design slices; any schema change
beyond nothing — this slice should need NO migration (roles are claims,
the catalog is code).
