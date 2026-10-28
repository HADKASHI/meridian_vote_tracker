# 02: Identity and the error contract

**What to build:** the API knows who is calling. An authentication handler reads `X-User-Id` and signs in the seeded user with a role claim; `Analyst` (both roles) and `Admin` policies guard endpoints. Unknown or missing users get 401 `unknown-user`, wrong roles 403 `forbidden`, both ProblemDetails with `traceId`, as is every other error. `GET /api/users` lists the users for the switcher, to anyone, because the switcher needs it before a user is chosen. Health endpoints report liveness and readiness (database and Blob). Logs are JSON with scopes (trace, user), and Application Insights switches on only when its connection string is set.

**Blocked by:** 01

**Status:** done

- [x] `GET /api/users` returns Dana, Omar and Lena with their roles, with or without a user
- [x] A missing or unknown `X-User-Id` gets 401 ProblemDetails of type `unknown-user` with a `traceId`
- [x] A known user is signed in with their role; a wrong role gets 403 ProblemDetails of type `forbidden` with a `traceId` (proven on the admin endpoints by ticket 03's role matrix)
- [x] Every ProblemDetails, including unexpected 500s, carries `traceId`
- [x] `/health/live` answers without dependencies; `/health/ready` checks PostgreSQL and Blob
- [x] JSON console logging with scopes is on outside Development; no client data is logged
