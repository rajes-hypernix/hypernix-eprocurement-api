# PROTOTYPE.md — the behavioural oracle

The folder `prototype/` contains the **original eProcure prototype** and its headless
test harnesses. The prototype is the **source of truth for behaviour and UX**. Every
slice you build must match what the prototype does. When the written docs and the
prototype seem to disagree, the prototype wins for *behaviour*; BUSINESS-RULES.md wins
for *governance that the prototype only simulated* (see note at the bottom).

```
prototype/
  eprocure-portal.html      <- the full clickable app (single-file vanilla JS)
  tests/
    e2e.js                  <- 50 assertions: full sourcing->P2P flow
    safety.js               <- 11 assertions: ASN/GRN/invoice quantity guards
    fixes.js                <- 7  assertions: award eligibility + bid deadline
    package.json
```

## Three ways to use it

**1. See it (UX parity).** Open `prototype/eprocure-portal.html` in a browser. Click
through the screen you're about to build. Match the layout, fields, columns, statuses,
buttons, and copy. (Design tokens are listed in ARCHITECTURE.md.)

**2. Read it (exact rules).** It's a single-file vanilla-JS app; all logic is in the
largest `<script>`. When you need the precise rule (tolerance %, status tokens, how a
total is computed, what a guard clamps), read the relevant function rather than guess.
Useful entry points: `awardableVids`, `vbSubmit` (deadline), `grnPost` (receipt caps),
`asnBuild`/`poRemainingToShip` (ship guards), `invBuild`/`invLineMatch` (3-way),
`confirmAward` (PO generation), `vchRelease` (payment).

**3. Run it (executable oracle).** The harnesses encode the exact expected outcomes.
Use them to confirm behaviour before/while you build the equivalent server logic:

```bash
cd prototype/tests
npm install            # installs jsdom
npm test               # runs e2e + safety + fixes (expect: 50/50, 11/11, 7/7)
# or individually:
npm run e2e
npm run safety
npm run fixes
```

If you're unsure what the prototype does in an edge case, write a tiny Node+jsdom probe
against `eprocure-portal.html` (same pattern as the harnesses): load it, call the global
function, assert the DOM/state. That gives you a definitive answer.

## Per-slice parity check (do this every slice)

When you finish a slice, before declaring it done:
1. Open the matching prototype screen and confirm the React screen matches (fields,
   columns, statuses, actions, empty states).
2. For any business rule in the slice, confirm your server enforces the **same outcome**
   the prototype produces (and that the relevant harness assertion still describes it).
3. Note in your summary: "Parity with prototype: <screen/flow> — matched" and call out
   any intentional difference.

## Important: where the build must EXCEED the prototype

The prototype enforces the quantity/match rules for real but only **simulates** the
governance rules in the browser. The production build must enforce these **server-side**
(they are in BUSINESS-RULES.md): segregation of duties + approval/DoA on award and
payment, bid-deadline as a server timestamp, evaluator masking bound to identity (not a
switchable role), full immutable audit trail, vendor access scoping, and queued/retryable
NetSuite pushes. So: match the prototype's behaviour, and additionally harden these.
