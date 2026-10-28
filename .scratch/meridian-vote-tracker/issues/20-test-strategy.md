# Set the test strategy

Type: grilling
Status: claimed
Blocked by: 12

## Question

Each slice is done only when its tests pass in CI ([Set the build order](12-build-order.md)). Decide:
- **Test projects and layers**: pure unit tests (classification, scoring); use-case modules against real PostgreSQL and Azurite through Testcontainers (never EF in-memory); API tests through `WebApplicationFactory`; any Angular tests
- **The required integration test**: the brief's 7.2 example as an end-to-end import scenario
- **Fixed tests**: the guard test, the decision race and the publication race (one success, one 409), the role matrix (401/403 for every write endpoint)
- **The timed synthetic ~1M-row import**: how the file is generated to mirror [Audit the historic file](17-historic-audit.md), where it runs (CI or on demand), and how time and peak memory are reported
- **Test data**: committed synthetic CSVs only; database isolation between tests
- **What CI runs** on every push
