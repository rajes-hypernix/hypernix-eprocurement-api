# ARCHITECTURE.md

## Layers (Clean architecture)

Dependencies point **inward**. Domain knows nothing about the outside world.

```
eProcure.Api            HTTP endpoints, DI, auth middleware, Swagger, request/response DTOs
   depends on
eProcure.Infrastructure EF Core DbContext, repositories, migrations, NetSuite client,
                        JWT issuing, sequence/code generator, audit writer
   depends on
eProcure.Application     use-cases (services), DTOs, FluentValidation validators,
                        interfaces (IRepository, INetSuiteClient, IClock, IAuditLog…)
   depends on
eProcure.Domain          entities, value objects, enums, invariants/guard rails
                        (pure C#, no framework references)
```

Rule of thumb: **business rules live in Domain + Application, never in controllers or
React.** Controllers are thin: validate → call an application service → return a DTO.

## Request flow (example: submit a bid)

1. `POST /api/rfqs/{id}/bids` hits `BidsController`.
2. Controller reads the authenticated principal (vendor) and request DTO.
3. Calls `SubmitBidCommand` in Application.
4. Application loads the RFQ aggregate, checks **deadline rule** (RFQ status must be
   `Open`), checks required answers, persists via repository, writes an **audit entry**.
5. Returns a `BidDto`. Controller maps to HTTP 200/400/409.

Guard rails are enforced in step 4 (Application/Domain), so they hold no matter which
client calls the API.

## Cross-cutting concerns (build once, use everywhere)

- **Audit trail** — `IAuditLog.Write(entity, action, before, after, actor)` called on
  every state transition. Stored in an append-only `AuditEntry` table. See BUSINESS-RULES.
- **Codes & IDs** — `ICodeGenerator.Next("RFQ")` → `RFQ-2026-0001` from a DB sequence;
  `Guid` PKs are entity-generated.
- **Clock** — `IClock.UtcNow` (injectable) so deadline logic is testable.
- **Current user** — `ICurrentUser` exposes id, role(s), and (for vendors) vendorId.
- **Authorization** — policy-based (`[Authorize(Policy="Buyer")]`, etc.) +
  resource-based checks (a vendor may only read its own records).

## Frontend

- Vite + React + TS. `web/src/api/` holds the **generated** types
  (`npm run gen:api` → openapi-typescript against `https://localhost:5001/swagger/v1/swagger.json`)
  and a thin `client.ts` fetch wrapper that attaches the JWT.
- TanStack Query for all server reads/writes (caching, loading, mutations).
- Screens mirror the prototype: Dashboard, Requisitions, RFQs (+ builder), Bid Openings,
  Awards & POs, Deliveries, Invoices, Payments, Statements, Vendor Master, Forms,
  Clarifications, and the vendor portal equivalents.
- Visual design: reuse the prototype's tokens — header teal `#336374`, active-nav
  `#25586B`, accent/links `#2C5C6E`, cream page `#F3EFE9`, greige panels `#F6F4EF`,
  hairline `#E4DED2`; square corners (circles only for avatars, status dots, the
  clarification launcher); Inter for UI, Georgia for page titles + numerals.

## Environments

- **Local:** Docker Postgres + `dotnet run` + `npm run dev`.
- **Dev/Staging:** Railway (ASP.NET Core service + managed Postgres). Same Docker image.
- **Production (SPSB):** Azure App Service / Container Apps + Azure Database for
  PostgreSQL Flexible Server, **Malaysia West** region for in-country data residency.
