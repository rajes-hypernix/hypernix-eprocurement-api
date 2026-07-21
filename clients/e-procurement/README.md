# Hypernix eProcure — client

Phase 0–1: original eProcure UI shell + FSH JWT/tenant auth + typed `/api/v1` clients.

## Run

```bash
# API must be up (HTTPS profile — https://localhost:7030)
npm install
npm run dev   # http://localhost:5175
```

Vite proxies `/api` → `https://localhost:7030` (`secure: false` for local dev cert).  
Do **not** point the proxy at `http://localhost:5030` — that port 307-redirects to HTTPS and the redirect drops the Bearer token (401s on every authenticated call).

Override with `VITE_API_BASE_URL` if needed.

## Layout

- `src/api/*` — platform, suppliers, sourcing, procurement, notifications
- `src/lib/fsh-permissions.ts` + `permissions-map.ts` — old actions → FSH (nav/`Gated` wiring = Phase 6)
- Design tokens/CSS/nav from original `eprocure/web`
