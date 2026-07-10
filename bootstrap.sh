#!/usr/bin/env bash
# bootstrap.sh — scaffold the eProcure .NET solution + React app + local Postgres.
# Run from inside the eprocure/ folder (where CLAUDE.md and docker-compose.yml live):
#   chmod +x bootstrap.sh && ./bootstrap.sh
set -euo pipefail

echo "==> Checking tools"
dotnet --version
node -v
docker --version
git --version

echo "==> git init + .gitignore"
[ -d .git ] || git init
cat > .gitignore <<'EOF'
bin/
obj/
*.user
appsettings.*.local.json
node_modules/
dist/
.env
.env.local
.vs/
.idea/
EOF

echo "==> .NET solution (Clean architecture)"
mkdir -p api && cd api
[ -f eProcure.sln ] || dotnet new sln -n eProcure
dotnet new classlib -n eProcure.Domain         -o src/eProcure.Domain         --force
dotnet new classlib -n eProcure.Application     -o src/eProcure.Application     --force
dotnet new classlib -n eProcure.Infrastructure  -o src/eProcure.Infrastructure  --force
dotnet new webapi    -n eProcure.Api            -o src/eProcure.Api  --use-controllers --force
dotnet new xunit     -n eProcure.Tests          -o tests/eProcure.Tests         --force
dotnet sln add src/eProcure.Domain src/eProcure.Application src/eProcure.Infrastructure src/eProcure.Api tests/eProcure.Tests

echo "==> project references (Api -> Infrastructure -> Application -> Domain)"
dotnet add src/eProcure.Application    reference src/eProcure.Domain
dotnet add src/eProcure.Infrastructure reference src/eProcure.Application
dotnet add src/eProcure.Api            reference src/eProcure.Infrastructure
dotnet add tests/eProcure.Tests        reference src/eProcure.Application src/eProcure.Infrastructure src/eProcure.Api

echo "==> NuGet packages"
dotnet add src/eProcure.Infrastructure package Microsoft.EntityFrameworkCore
dotnet add src/eProcure.Infrastructure package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add src/eProcure.Infrastructure package Microsoft.EntityFrameworkCore.Design
dotnet add src/eProcure.Api package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add src/eProcure.Api package Swashbuckle.AspNetCore
dotnet add src/eProcure.Application package FluentValidation
dotnet add tests/eProcure.Tests package FluentAssertions
dotnet add tests/eProcure.Tests package Microsoft.EntityFrameworkCore.InMemory
dotnet tool install --global dotnet-ef 2>/dev/null || dotnet tool update --global dotnet-ef
cd ..

echo "==> React app (Vite + TypeScript)"
npm create vite@latest web -- --template react-ts
cd web
npm install
npm install @tanstack/react-query
npm install -D openapi-typescript vitest @testing-library/react @testing-library/jest-dom jsdom
cd ..

echo "==> Local Postgres (Docker)"
docker compose up -d

echo "==> Prototype harness deps (the behavioural oracle)"
( cd prototype/tests && npm install )

echo "==> First commit"
git add -A
git commit -m "chore: scaffold eProcure (api + web + docs + docker + prototype)" || true

echo ""
echo "DONE. Next:"
echo "  1. Open this folder in Claude Code (it reads CLAUDE.md automatically)."
echo "  2. Paste the Phase 0 prompt from docs/DEMO.md."
echo "  3. After it builds: API on https://localhost:5001 (/swagger), web on http://localhost:5173"
