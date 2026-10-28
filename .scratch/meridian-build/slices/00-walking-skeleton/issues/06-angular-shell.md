# 06: Angular shell

**What to build:** the web app (Angular 22, standalone, zoneless, Material) generates its client with orval from `contracts/openapi.json` and shows the whole navigation. The header user switcher (per-tab choice in `sessionStorage`, new tabs from the last choice in `localStorage`) feeds an interceptor that adds `X-User-Id` to every `/api/` call; a 401 opens the switcher. Every route has a placeholder page in its feature folder, `/` redirects to `/review`, and `**` is a not-found page. The header shows the jobs indicator ("2 jobs running · 1 queued", linking to `/jobs`) and the Review badge from one `GET /api/status` poll; `/jobs` lists jobs. Shared helpers: `problemToFieldErrors`, error notices showing `traceId`, the 409 card, and `pollWhile`. Features import only core, shared and the generated API (lint-enforced).

**Blocked by:** 03, 04

**Status:** done

- [x] `npm run build` generates the client and builds; `npm run lint` and `npm test` pass
- [x] The interceptor adds `X-User-Id` only to `/api/` calls (tested with `HttpTestingController`)
- [x] The switcher keeps a per-tab user across reloads and starts new tabs as the last choice
- [x] Every route of the spec resolves to its placeholder page
- [x] The jobs indicator and Review badge reflect `GET /api/status`; polling stops when nothing is active and the tab is hidden (tested)
- [x] `problemToFieldErrors` maps camelCase JSON-path keys to fields and unknown keys to a form error (tested); 5xx notices show the `traceId`
- [x] A feature folder importing another feature folder fails lint
