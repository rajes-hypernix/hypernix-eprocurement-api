# SETUP.md — one-time terminal setup

Run these once to create the repo and all project scaffolding. Copy the whole block,
or step through section by section. Assumes you have installed: **git**, **.NET 10 SDK**
(`dotnet --version`), **Node 20+** (`node -v`), and **Docker Desktop**.

> You can either run the commands below yourself, OR drop the `CLAUDE.md` + `docs/`
> files in and let Claude Code run Slice 0 (it does all of this). Doing it once by hand
> is useful so you understand the layout.

---

## 1. Create the repo and drop in the reference files

```bash
mkdir eprocure && cd eprocure
git init

# put the kit files in place (CLAUDE.md, SETUP.md, docker-compose.yml, docs/*, prototype/*)
mkdir -p docs prototype/tests
# (copy CLAUDE.md, SETUP.md, docker-compose.yml into ./ , the docs/*.md into ./docs/ ,
#  and the prototype/ folder — eprocure-portal.html + tests/ — into ./prototype/)
```

`.gitignore` (root):

```bash
cat > .gitignore <<'EOF'
# .NET
bin/
obj/
*.user
appsettings.*.local.json
# Node
node_modules/
dist/
.env
.env.local
# IDE
.vs/
.idea/
EOF
```

---

## 2. Create the .NET solution (Clean architecture)

```bash
mkdir api && cd api
dotnet new sln -n eProcure

# projects
dotnet new classlib -n eProcure.Domain        -o src/eProcure.Domain
dotnet new classlib -n eProcure.Application    -o src/eProcure.Application
dotnet new classlib -n eProcure.Infrastructure -o src/eProcure.Infrastructure
dotnet new webapi    -n eProcure.Api           -o src/eProcure.Api
dotnet new xunit     -n eProcure.Tests         -o tests/eProcure.Tests

# add to solution
dotnet sln add src/eProcure.Domain src/eProcure.Application src/eProcure.Infrastructure src/eProcure.Api tests/eProcure.Tests

# project references (dependency direction: Api -> Infrastructure -> Application -> Domain)
dotnet add src/eProcure.Application    reference src/eProcure.Domain
dotnet add src/eProcure.Infrastructure reference src/eProcure.Application
dotnet add src/eProcure.Api            reference src/eProcure.Infrastructure
dotnet add tests/eProcure.Tests        reference src/eProcure.Application src/eProcure.Infrastructure src/eProcure.Api
```

## 3. Add packages

```bash
# EF Core + PostgreSQL (Infrastructure)
dotnet add src/eProcure.Infrastructure package Microsoft.EntityFrameworkCore
dotnet add src/eProcure.Infrastructure package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add src/eProcure.Infrastructure package Microsoft.EntityFrameworkCore.Design

# Auth + Swagger (Api)
dotnet add src/eProcure.Api package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add src/eProcure.Api package Swashbuckle.AspNetCore

# Validation (Application)
dotnet add src/eProcure.Application package FluentValidation

# Test helpers
dotnet add tests/eProcure.Tests package FluentAssertions
dotnet add tests/eProcure.Tests package Microsoft.EntityFrameworkCore.InMemory

# EF CLI (global, one-time)
dotnet tool install --global dotnet-ef    # or: dotnet tool update --global dotnet-ef
cd ..
```

> **To use SQL Server instead of Postgres:** swap the Npgsql package for
> `Microsoft.EntityFrameworkCore.SqlServer`, change `UseNpgsql(...)` to
> `UseSqlServer(...)`, and update the connection string. Nothing else changes.

---

## 4. Create the React app (Vite + TypeScript)

```bash
# from repo root
npm create vite@latest web -- --template react-ts
cd web
npm install
npm install @tanstack/react-query
npm install -D openapi-typescript vitest @testing-library/react @testing-library/jest-dom jsdom
cd ..
```

---

## 5. Local PostgreSQL (Docker)

`docker-compose.yml` (root) — already included in the kit:

```yaml
services:
  db:
    image: postgres:16
    environment:
      POSTGRES_USER: eprocure
      POSTGRES_PASSWORD: localdev
      POSTGRES_DB: eprocure
    ports:
      - "5432:5432"
    volumes:
      - pgdata:/var/lib/postgresql/data
volumes:
  pgdata:
```

```bash
docker compose up -d        # starts Postgres on localhost:5432
```

Connection string (API `appsettings.Development.json`, created in Slice 0):

```
Host=localhost;Port=5432;Database=eprocure;Username=eprocure;Password=localdev
```

> **Fresh clone:** `appsettings.Development.json` is gitignored (it holds the local JWT key
> and DB password). Copy the template and fill in the `REPLACE_ME` placeholders before running
> the API: `cp api/src/eProcure.Api/appsettings.Development.example.json api/src/eProcure.Api/appsettings.Development.json`

---

## 6. First commit

```bash
git add -A && git commit -m "chore: scaffold eProcure (api + web + docs + docker)"
```

You now have an empty-but-wired solution. Hand the repo to Claude Code and run the
**Slice 0** prompt from `docs/BUILD-PLAN.md`.
