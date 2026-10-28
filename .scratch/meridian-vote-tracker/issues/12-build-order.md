# Set the build order

Type: grilling
Status: open
Blocked by: 02, 03, 04, 05, 06, 08, 09, 10, 11, 15, 16

## Question

In what order are the vertical slices built, so that a partial submission is still coherent and the order itself shows good prioritisation (the brief says the order is assessed)? What extras make it stand out without risking correctness, and where is the line for the bonus?

## Comments

- From [Design background jobs and live progress](10-background-jobs.md): compose gains `worker` and `azurite` services, and the integration tests need Azurite via Testcontainers. The Jobs list and the header indicator are UI to schedule.
- From [Design snapshots](11-snapshots.md): candidate extra: comparing two snapshots (rank movement between quarters, or a publication and the one it superseded), a pure read over stored snapshot lines. The create dialog has a preview step (vote count, blocking "no position" list with "Remove these votes", warnings).
