# Hypernix eProcure — client

Phase 0 scaffold: original eProcure UI shell + FSH JWT/tenant auth against the new API.

## Run

```bash
# API must be up (default http://localhost:5030)
npm install
npm run dev   # http://localhost:5175
```

Sign in with a `root` tenant admin account. After login you should see the original topbar/sidebar shell; screens are placeholders until later phases.

## Notes

- Design tokens/CSS/nav copied from original `eprocure/web` (visual fidelity).
- Auth/API client patterns adapted from `clients/admin` / `clients/dashboard`.
- `X-FSH-App: eprocurement` (not `dashboard`) so root SuperAdmin login is allowed.
