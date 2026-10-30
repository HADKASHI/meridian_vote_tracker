# 01: Playwright smoke test in CI

**What to build:** one browser smoke test against the running compose stack, across the merged slices: a user switches identity in the header, uploads a small committed synthetic export through the Imports page, sees its report with outcome counts, then two tabs open the same pending conflict and both decide it. Exactly one decision is accepted and the other tab shows the "already decided" 409 card in place. The test runs in CI on every push, in the compose job after the stack is up, and locally with one documented command.

**Blocked by:** None (can start immediately)

**Status:** done

- [x] The test drives the real UI on the compose stack (nginx, API, worker, database, Azurite), using only committed synthetic files
- [x] Switching user in the header changes who the app acts as, and the upload is made as that user
- [x] The upload finishes, and the report shows the outcome counts the fixture is built to give
- [x] Two tabs decide one conflict: one succeeds, the other shows the already-decided card, never a toast or a retry
- [x] It runs in CI after the compose stack starts, with the Playwright version pinned in `e2e/`, and fails the build when it fails
- [x] It runs locally with one command against a stack started as the README says
