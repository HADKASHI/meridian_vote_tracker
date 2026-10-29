# 01: Capture every screen with synthetic data

**What to build:** a development-only Playwright script (never run in CI; slice 8 reuses its setup for the smoke test) that, against `docker compose up` with the synthetic member states, fills the database with synthetic data (the `seed-demo` command, then a committed original CSV and a revision of it uploaded through the Imports page) and captures every screen at 1280 px and 390 px, in light and dark. For each capture it reports whether the page scrolls sideways. Run once on the current `main` look, it commits the "before" screenshots so the PR can show before and after.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] One command brings the stack up with synthetic data only (no file from `candidate_data/`) and captures every screen of the slice's lane
- [ ] The original and revision CSVs are committed, synthetic, and produce new, unchanged, duplicate, invalid, contradiction and conflict rows plus an incomplete-vote warning
- [ ] Each capture is taken at 1280 px and 390 px, in light and dark, with the page's sideways overflow reported
- [ ] The "before" screenshots are committed under `docs/screens/before/`
- [ ] The script lives outside the Angular build, lint and Vitest run, and CI is unchanged
