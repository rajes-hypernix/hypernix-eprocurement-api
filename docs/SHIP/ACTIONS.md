# ACTIONS — do these in order

Everything for this round, sequenced. Two things to place, one optional test pass, then the CF-FIX-3
build. Adjust `~/dev/eprocure` if your repo path differs.

## 1. Place both docs into the repo

Unzip the package into docs/ (it lands as docs/SHIP/ and docs/CF-FIX-3/):

```
unzip -o ~/Downloads/ship-round.zip -d ~/dev/eprocure/docs/
```

Move the roadmap to the repo root docs level and commit both (they're reference docs, safe to commit):

```
cd ~/dev/eprocure
git add docs/SHIP/CUSTOMIZATION-ROADMAP.md docs/CF-FIX-3/PROMPT-CF-FIX-3.md
git commit -m "docs: customization-layer roadmap + CF-FIX-3 plan (fields/lists lifecycle)"
```

## 2. (RECOMMENDED) Consolidation test pass before the big slice

You now have three fix-rounds stacked (v1.6, v1.7, and about to add v1.8). Before pouring the
biggest architectural slice on top, confirm the three sit together cleanly on YOUR machine:

```
cd ~/dev/eprocure && git status --short
```
(should be empty)

```
cd ~/dev/eprocure/api && dotnet test 2>&1 | tail -5
```
```
cd ~/dev/eprocure/web && npx vitest run 2>&1 | tail -5
```
```
cd ~/dev/eprocure/e2e-audit && npx playwright test 2>&1 | tail -12
```

Expect roughly 506 / 237 / 86. If all green, the three rounds coexist and you have a clean baseline
to build CF-FIX-3 on. If anything's red, THAT is finding #1 — send it to me before proceeding.

Optional but worth 10 minutes: open the app and eyeball the searchable select against your NetSuite
reference screenshots — the one thing that's functionally proven but not yet aesthetically signed
off by your eye. (Vendor Master country/state/city is a good place — pick country, watch state
narrow.)

## 3. Kick off CF-FIX-3 in Claude Code

Paste:

> Read `docs/CF-FIX-3/PROMPT-CF-FIX-3.md` and start with Step 0 — the full file plan for all six
> tasks — and wait for my confirmation before writing code. Pay special attention to the
> two-registry architecture (the anti-rot foundation), the form-extensibility requirement on the
> entry-form reference provider, and the extensibility proof (T6). Do not begin coding until I
> confirm the Step 0.

## 4. When Claude Code returns Step 0

Send it to me (Claude in this chat). I'll pressure-test it before any code is written — the two
things I'll check hardest are the `PurgeCustomFieldHistory` permission tier and the fail-closed
status rule (unknown record status = treated as live = never purgeable), because those are the
safety floor.

## 5. Standing housekeeping (whenever convenient, not blocking)

While you're near the Azure portal at some point: grab the **production Postgres version** and add a
line to `ARCHITECTURE.md`. It's been asked for repeatedly and gates a later CF6 simplification. Two
minutes, ends a recurring open item.

---

### The order, in one line
Place docs → commit → confirm the 3 rounds are green on your machine → send CF-FIX-3 Step 0 to Claude
Code → send its Step 0 back to me → build after I confirm.
