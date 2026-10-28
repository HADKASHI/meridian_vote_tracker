# 05: Fixture builders and test helpers

**What to build:** later slices arrange their data without each other's code. Fixture builders write straight into the database: approved vote details and country votes (origin: a fixture import and its row records), pending and decided conflicts (ballot and details), positions, and calculated snapshots with lines, results and publications. A barrier helper releases two concurrent calls at once for race tests. A sample integration test arranges data with a builder and reads it through the API. A Development-only `seed-demo` command runs the same builders against the local database for checking screens by hand.

**Blocked by:** 01

**Status:** ready-for-agent

- [ ] Builders create approved values whose origin is a fixture import row (file, line, uploader), pending and decided conflicts, positions, and a calculated snapshot with its votes, lines, results and an optional publication, all satisfying the schema's constraints and guard
- [ ] The barrier helper releases N tasks together (tested with two)
- [ ] A sample integration test uses the template database and a fixture builder through the HTTP API
- [ ] `seed-demo` runs only in Development and fills an empty local database with a small demo set
