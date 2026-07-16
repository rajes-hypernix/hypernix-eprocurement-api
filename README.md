# Hypernix eProcurement API

FSH-based backend for eProcurement, migrated from the legacy POC API.

## Repo layout

| Path | Purpose |
|---|---|
| `BuildingBlocks/`, `Host/`, `Modules/`, `Tests/`, `Tools/` | .NET API (FullStackHero modular monolith) |
| `clients/admin` | FSH operator console — **reference UI** for migration |
| `clients/dashboard` | FSH tenant dashboard — **reference UI** for migration |
| `deploy/docker` | Local full-stack via Docker Compose |
| `deploy/terraform` | AWS deployment (ECS, RDS, Redis, ALB) |

Your production frontend will eventually replace the FSH clients. Keep them here as patterns for auth, tenancy, and API integration.

## Quick start (API only)

```bash
dotnet restore FSH.Starter.slnx
dotnet build Host/FSH.Starter.Api/FSH.Starter.Api.csproj
```

## Quick start (full stack with Docker)

```bash
cd deploy/docker
cp .env.example .env
# edit .env — set JWT_SIGNING_KEY, SEED_ADMIN_PASSWORD, passwords, URLs
docker compose up -d --build
```

- API: `http://localhost:8080`
- Admin (reference): `http://localhost:8081`
- Dashboard (reference): `http://localhost:8082`

## Branch

Active development: `api-fsh-only`
