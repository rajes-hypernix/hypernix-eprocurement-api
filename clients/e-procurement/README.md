# Hypernix eProcure — client

Primary buyer + vendor SPA for Hypernix eProcurement (FSH JWT/tenant auth, `/api/v1`).

See also: [docs/CUTOVER.md](../../docs/CUTOVER.md).

## Run (local)

```bash
# API must be up on HTTPS — https://localhost:7030
npm ci
npm run dev   # http://localhost:5175
```

Uses `public/config.json` with empty `apiBase` so the Vite proxy handles `/api` → `https://localhost:7030` (`secure: false` for the local dev cert).  
Do **not** point the proxy at `http://localhost:5030` (307 to HTTPS drops the Bearer token).

Override proxy origin with `VITE_API_BASE_URL` (see `.env.example`).

## Build for deploy (manual)

Runtime `dist/config.json` is chosen by build script:

| Command | Config file | `apiBase` |
|---------|-------------|-----------|
| `npm run dev` | `public/config.json` | empty → local proxy |
| `npm run build:dev` | `public/config.dev.json` | remote dev API |
| `npm run build` / `build:prod` | `public/config.prod.json` | set before prod go-live |

```bash
npm run build:dev   # deploy this dist/ to the development web host
```

Edit `public/config.dev.json` / `public/config.prod.json` to change API URLs — do not put secrets there.

**IIS:** `public/web.config` is copied into `dist/` on every build. It rewrites SPA routes (`/login`, `/reset-password`, …) to `index.html`. Install the [IIS URL Rewrite](https://www.iis.net/downloads/microsoft/url-rewrite) module on the server, or deep links / password-reset redirects will 404.

## Production / Docker

Runtime config is rendered into `/config.json` at container start (`apiBase`, `defaultTenant`).  
Image: build from this folder (`Dockerfile`); compose service publishes host port **8083** by default.

## Layout

- `src/api/*` — platform, suppliers, sourcing, procurement, notifications, search
- `src/lib/fsh-permissions.ts` + `permissions-map.ts` — old actions → FSH (`gateNav` / `<Gated>`)
- Design tokens/CSS/nav fidelity from original eprocure/web

## Tests

```bash
npm run test:e2e   # Playwright, route-mocked (no live API required)
```
