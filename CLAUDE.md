# CLAUDE.md — eProcure build instructions

You are building **eProcure**, a Source-to-Pay-to-Settle e-procurement web app for
**Hypernix Sdn Bhd**, first client **Sarawak Petchem (SPSB)**, an oil & gas company
on NetSuite ERP. This is production software people stake audited award and payment
decisions on.

A working **vanilla-JS prototype** already exists and is the behavioural spec. We are
re-implementing it properly as React + .NET. When in doubt about *what a screen does*,
the prototype is the source of truth for UX and business rules. This repo is the
*production* rebuild, not a port of the prototype's code.

---

## Golden rules (do not violate)

1. **All business logic lives server-side.** The React app is a rendering layer only.
   Every approval, quantity guard, match, and state transition is enforced in the API.
2. **Enforce every guard rail in `docs/BUSINESS-RULES.md`.** These are non-negotiable
   and were the findings of a formal audit of the prototype. In particular: 3-way
   match, ASN/GRN quantity guards, award eligibility (technically-passed only),
   bid-deadline enforcement, segregation of duties + approval workflow, immutable
   audit trail on every state change, vendor data scoping.
3. **Money is `decimal`, never `float`/`double`.** Postgres `numeric(18,2)`.
4. **IDs:** every entity has a `Guid Id` primary key (server-generated) PLUS a
   human-readable `Code` (e.g. `RFQ-2026-0001`) generated from a server-side sequence.
   Never use array length or random numbers to generate IDs.
5. **No secrets in code.** Connection strings, JWT keys, NetSuite creds come from
   configuration / environment variables only.
6. **Every slice ships with tests.** xUnit for the API, Vitest for the web. A slice is
   not done until `dotnet test` and the web build/tests pass and it runs on localhost.
7. **Never weaken a guard rail to make a test pass.** If a rule blocks something, that
   is correct behaviour — fix the test, not the rule.
8. **Match the prototype.** `prototype/eprocure-portal.html` is the behavioural + UX
   source of truth. Before and after building a screen/flow, check it against the
   prototype (see `docs/PROTOTYPE.md`). You may open it, read its `<script>` for exact
   rules, or run its harnesses (`cd prototype/tests && npm test`) as an executable
   oracle. The build must match the prototype's behaviour AND additionally enforce the
   governance rules the prototype only simulates (see `docs/BUSINESS-RULES.md`).

---

## Testing mandate (read every session — no exceptions)

The goal is **all of the prototype's current functions working as a real .NET
application on the user's localhost.** Not a mock, not a partial — the real behaviour.

- **Test against `prototype/eprocure-portal.html` continuously.** After every change,
  check your work against the prototype (open it, read its `<script>`, and/or run
  `cd prototype/tests && npm test`). Keep iterating until the .NET + React behaviour
  matches the prototype for that slice.
- **Keep testing until it's green AND matches.** `dotnet test` green is necessary but
  not sufficient — the outcome must equal the prototype's outcome. If they differ, you
  are not done.
- **It must run on localhost.** Every slice ends with the user able to open
  `http://localhost:5173`, click through, and see the real feature working.
- Do not claim a slice is done with excuses ("would work with a backend", "mock for
  now"). If something can't be verified, say so explicitly and keep working.

## Integration scope — FOR NOW (important)

NetSuite integration is **OFF for now.** In production, Vendors, Requisitions, and
some downstream records originate from NetSuite — ignore that for now and use local
data instead:

- **Vendors → seed dummy vendors** (see `docs/SEED-DATA.md`). No NetSuite calls.
- **Requisitions (PRs) → seed dummy requisitions** (see `docs/SEED-DATA.md`). No NetSuite.
- **Payment Vouchers → leave blank for now.** Build the screen as an empty placeholder
  (nav entry + "Coming soon / no data" state). Do not build PV logic yet.
- **All NetSuite push/pull → stub only** (an interface that no-ops or logs). Do not
  implement real integration until explicitly asked.

Everything else (RFQ, bidding, evaluation, award, PO, ASN, GRN, Invoice, Statements)
is built for real per the prototype.

## Stack (pinned)

- **API:** ASP.NET Core Web API on **.NET 10**, C#, Clean-architecture layering
  (Domain / Application / Infrastructure / Api).
- **ORM:** **EF Core 10** with **Npgsql** (PostgreSQL provider).
- **Database:** **PostgreSQL 16** (local via Docker Compose; Railway/Azure managed in
  deployment). To switch to SQL Server later: change the provider package + the
  `UseNpgsql(...)` line + connection string only.
- **Frontend:** **React + TypeScript** built with **Vite**, **TanStack Query** for
  server state, typed API client generated from the API's OpenAPI/Swagger via
  **openapi-typescript**.
- **Auth:** JWT bearer with role claims (buyer, tech, comm, admin, vendor). Vendors are
  a separate principal type from internal users (see SoD rule).
- **Tests:** xUnit (+ FluentAssertions) for API; Vitest for web.

---

## Repo layout

```
eprocure/
  CLAUDE.md                 <- this file
  SETUP.md                  <- one-time terminal setup
  docker-compose.yml        <- local Postgres
  docs/
    ARCHITECTURE.md         <- layers, folders, request flow
    DATA-MODEL.md           <- entities + relationships
    BUSINESS-RULES.md       <- the enforced rules (audit-derived)
    CONVENTIONS.md          <- coding standards, naming, testing
    BUILD-PLAN.md           <- ordered vertical slices + the prompt for each
    PROTOTYPE.md            <- how to use the prototype as the behavioural oracle
  prototype/
    eprocure-portal.html    <- the original clickable app = behavioural source of truth
    tests/                  <- headless harnesses (e2e/safety/fixes) = executable oracle
  api/
    eProcure.sln
    src/
      eProcure.Domain/          <- entities, enums, domain rules (no deps)
      eProcure.Application/     <- use-cases, DTOs, validators, interfaces
      eProcure.Infrastructure/  <- EF Core DbContext, repositories, NetSuite, auth
      eProcure.Api/             <- controllers/endpoints, DI, Program.cs, Swagger
    tests/
      eProcure.Tests/           <- xUnit
  web/
    src/                        <- React app (Vite + TS)
```

---

## How to run locally

```bash
# 1. start the database
docker compose up -d

# 2. API  (from api/)
cd api
dotnet ef database update --project src/eProcure.Infrastructure --startup-project src/eProcure.Api
dotnet run --project src/eProcure.Api          # serves https://localhost:5001, Swagger at /swagger

# 3. Web  (from web/, new terminal)
cd web
npm run dev                                     # serves http://localhost:5173
```

## How to test

```bash
cd api && dotnet test
cd web && npm run test
```

---

## Workflow (how we build together)

We build in **vertical slices** defined in `docs/BUILD-PLAN.md`. Each slice = one
feature end-to-end (DB → API → React → tests). For each slice:

1. Read the slice section in `docs/BUILD-PLAN.md` and the rules it references in
   `docs/BUSINESS-RULES.md`.
2. Implement: entities + migration, application service + validation, API endpoints,
   React screen wired through the typed client, and tests.
3. Run `dotnet test` and `npm run test`; fix until green.
4. Print the exact commands to view it on `localhost` and a short "what to click to
   verify" checklist.
5. **Parity check:** open the matching screen in `prototype/eprocure-portal.html` (and,
   for rules, run or read the relevant harness in `prototype/tests/`); confirm the React
   screen and the server outcomes match the prototype. State "Parity with prototype:
   <screen/flow> — matched" and note any intentional difference.
6. Stop. The human checks localhost, then asks for the next slice.

Do **one slice per session.** Do not jump ahead. Keep changes scoped to the slice.

When you start a slice, first restate: the slice goal, the entities/endpoints/screens
you'll create, and the business rules you must enforce. Then build.
