# Playwright end-to-end flows

`@playwright/test` is intentionally NOT in the main `frontend/package.json`; it lives in this folder's own
`package.json` so normal installs/builds are unaffected.

```bash
cd frontend/e2e
npm install
npm run install:browsers
# app already running via docker compose (web on :3000, api on :8080):
E2E_NO_WEBSERVER=1 E2E_BASE_URL=http://localhost:3000 \
E2E_USER_EMAIL=... E2E_USER_PASSWORD=... E2E_ADMIN_EMAIL=... E2E_ADMIN_PASSWORD=... npm test
# or let Playwright start `vite dev` on :5173 (needs the API reachable via the vite proxy, default :8080)
```

## Required seed accounts (env vars, never committed)
| Variable | Purpose | Seed source |
|---|---|---|
| `E2E_USER_EMAIL` / `E2E_USER_PASSWORD` | pet owner: login, pets, vet booking, checkout, adoption request | demo user (e.g. `maya.rahman@example.com`) from `DemoDataSeeder` / `deploy/seed-demo.sql` |
| `E2E_ADMIN_EMAIL` / `E2E_ADMIN_PASSWORD` | admin approval | demo admin (e.g. `admin.farhana@example.com`) or the SuperAdmin from `SuperAdmin__Email/Password` |
| `E2E_VET_*`, `E2E_STORE_*` | reserved for future vet/store-owner flows | `dr.nusrat@example.com`, `tanvir.petmart@example.com` |

The demo password is defined in `backend/src/Pawzaroo.Infrastructure/Persistence/Seed/DemoDataSeeder.cs`; pass it via
the environment (CI uses repository secrets). Tests needing an account are skipped when its variables are unset.

Prerequisite data: demo data seeded (vets with slots, products, a listing, ideally a pending listing for admin approval).
Spec files are named `*.e2e.ts` so the main `vitest run` (which globs `*.spec.ts`) never picks them up.
Locators are grounded in the real page sources (English labels/roles); each spec header names the source files. Steps that could not be grounded were removed (see comments).
The specs have not been run against a live UI: expect some tuning on first run.
