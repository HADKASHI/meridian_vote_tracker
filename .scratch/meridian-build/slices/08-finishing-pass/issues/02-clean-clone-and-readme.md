# 02: Clean-clone run and README

**What to build:** a reviewer clones the repo, follows only the README and has the whole system running with `docker compose up --build`. The README explains what the app is, what to put in `candidate_data/` (or how to use the synthetic set), the demo users and roles and the identity shortcut, the screens to try, how to run the backend, frontend and e2e tests, the on-demand real-data and performance tests, the Azure run (pointing to its doc), and where the decisions, ADRs, schema and AI usage notes live. It never mentions the sandbox proxy override.

**Blocked by:** 01 (the README documents the smoke test's command)

**Status:** ready-for-agent

- [ ] `docker compose up --build` on a fresh clone, following only the README, starts every service and the app answers
- [ ] The README covers running, data, users and roles, tests (including the e2e smoke test), the Azure run and the document map
- [ ] It is accurate to the code: every command and path in it is checked
- [ ] It does not mention the sandbox override or `.claude/sandbox/`
