# CF-FINAL AUTO-DECISIONS (decision · why · how to reverse)

## AD-1 · Persist the EFFECTIVE resolved form, not just an explicit picker choice (T1)
- **Decision:** `PrForm.body()` sends `entryFormId: form.formId` (the form actually rendered), and reopen initializes the picker from `pr.entryFormId`. So every PR pins the exact layout it was entered on, even if the buyer never touched the picker.
- **Why:** the bug is silent reversion. Pinning the effective form makes a reopened PR immune to later role-preference changes and guarantees no phantom fields. Persisting only an explicit choice would leave "never-touched" PRs re-resolving to whatever the role default becomes later.
- **Reverse:** send `chosenFormId` instead of `form.formId` in `body()`.

## AD-2 · Remove the ungoverned residual custom/segment sections on PrForm (T1)
- **Decision:** deleted the `<CustomFieldsSection>`/`<SegmentsSection>` residual renders below the sublist on PrForm. A viewed PR now shows ONLY its chosen form's placed fields/segments.
- **Why:** those sections dumped EVERY applicable-but-unplaced custom field/segment — the exact "phantom fields" symptom the ledger names. The governed model (the form decides what shows) supersedes D5's zero-deploy residual. Other surfaces (PO, Vendor) keep their sections unchanged.
- **Reverse:** restore the two lines + their imports at the end of PrForm's TransactionPage children.

## AD-3 · Fixed 4 pre-existing `tsc -b` errors (the real gate was masked)
- **Decision:** the committed tree failed `npx tsc -b` (the ledger's gate) on 4 errors, because root `tsconfig.json` has `files: []` so the previously-used `tsc --noEmit` checked NOTHING (vacuously green). Repaired as pure type-safety, no behavior change: Button gains a `title?` passthrough (AdminCustomFields tooltip now valid); `Record<string,JSX.Element>`→`ReactElement` in PrForm; `result.id!` in PrForm; `sent!` in AdminEntryForms.test.
- **Why:** `tsc -b` green is the stated gate AND `npm run build` uses it — a red build is a broken handover. Correctness-first.
- **Reverse:** revert the four one-line touch-ups; note the build will go red again.
