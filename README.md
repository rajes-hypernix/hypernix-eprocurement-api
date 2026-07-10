# eProcure — production rebuild (React + .NET 10 + PostgreSQL)

This repo rebuilds the audited eProcure prototype as a real React + ASP.NET Core app.

## Read in this order
1. **SETUP.md** — create the repo, projects, and local Postgres (terminal, one-time).
2. **CLAUDE.md** — the rules Claude Code follows on every session.
3. **docs/BUILD-PLAN.md** — the ordered slices and the prompt to paste for each.
4. **docs/BUSINESS-RULES.md** — the guard rails (audit-derived, non-negotiable).
5. **docs/SEED-DATA.md** — the dummy Vendors + Requisitions to seed locally (no NetSuite).
6. **docs/DATA-MODEL.md**, **docs/ARCHITECTURE.md**, **docs/CONVENTIONS.md** — reference.
7. **docs/PROTOTYPE.md** — how to use `prototype/eprocure-portal.html` as the behavioural
   oracle (open it, read it, or run `prototype/tests` to confirm exact expected outcomes).

## Scope for now (integration OFF)
Vendors + Requisitions are **dummy local seed data** (no NetSuite). **Payment Vouchers
are left blank** (placeholder screen only). NetSuite is a **stub**. Everything else is
built for real and must match the prototype. Claude Code is instructed to **test against
the prototype every slice and keep iterating until localhost matches it.**

## The loop
1. Paste a slice prompt from `docs/BUILD-PLAN.md` into Claude Code.
2. Claude Code restates the plan, builds DB + API + React + tests, runs the tests.
3. It prints the localhost run commands + a verify checklist.
4. You check `http://localhost:5173`; if good, paste the next slice prompt. Repeat.

## Stack
React + TypeScript (Vite) · ASP.NET Core (.NET 10) · EF Core 10 · PostgreSQL 16 ·
JWT auth · NetSuite integration via a queued client. Dev/staging on Railway, production
on Azure (Malaysia West) — same Docker image. To switch DB to SQL Server: one provider
line + connection string.
