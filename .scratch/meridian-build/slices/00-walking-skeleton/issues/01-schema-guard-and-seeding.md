# 01: Schema, guard and seeding

**What to build:** the backend solution exists with its data project and both test projects. Migration 1 creates the whole locked schema (`docs/schema.md`): reference, ingestion, approved data, conflicts, snapshots and publication, and `job`, with the shared `history_seq`, every CHECK, composite FK and partial unique index, the four views, the guard trigger on every table but the allowlist, and the three seeded users. After every migrate, the member-state seeder loads `Seed:MemberStatesPath` when `member_state` is empty and fails with a message naming the missing file. Integration tests run against one PostgreSQL container with a migrated template database and a fresh copy per test.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] The solution builds with warnings as errors; a domain test project and an integration test project (xUnit v3, built-in `Assert`) exist
- [ ] Migration 1 holds the whole schema and the model has no pending changes
- [ ] The guard test finds every table from the catalog: each one outside `job`, `vote_current`, `country_vote_current` and the migrations history has the guard trigger, and UPDATE, DELETE and TRUNCATE on it are refused
- [ ] The seeder loads the committed synthetic `tests/data/member_states.csv` (codes, names, membership periods) through both the sync and async seeding hooks, only when `member_state` is empty
- [ ] With the file missing, migrating fails with a message naming the file and its folder
- [ ] `dana`, `omar` (analyst) and `lena` (admin) exist after migrating
- [ ] Each integration test gets its own database copied from the migrated template
