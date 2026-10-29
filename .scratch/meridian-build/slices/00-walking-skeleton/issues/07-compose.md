# 07: Docker Compose

**What to build:** `docker compose up --build` on a clean clone with `member_states.csv` in `candidate_data/` starts the six services (`db`, `azurite`, `migrate`, `api`, `worker`, `web`) in health-checked order, with no `.env`. One backend image has three entry points (migration bundle, API, worker); nginx serves the SPA and proxies `/api/` on the same origin. Host ports bind to localhost only. A committed `.env.example` lists every variable. The sandbox's proxy override lives in a separate file passed with `-f`.

**Blocked by:** 04, 06

**Status:** done

- [x] `docker compose up --build` with the synthetic member states starts all six services; `migrate` exits 0 and `api` is healthy
- [x] Without `member_states.csv`, `migrate` exits non-zero with a message naming the file, and `api` and `worker` don't start
- [x] `http://localhost:8080/` serves the app and `/api/users` answers through nginx
- [x] A no-op job queued in the running stack is claimed by the `worker` container
- [x] `candidate_data/`, `.env`, `.git` and `.scratch` never reach a build context
