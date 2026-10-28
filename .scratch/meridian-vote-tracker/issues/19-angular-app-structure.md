# Shape the Angular app

Type: prototype
Status: open
Blocked by: 12

## Question

How is the Angular 22 app structured, and how should its screens look and behave? Cover:
- routing for the screens: import and report, Review inbox, vote page with timeline, positions, snapshots and publication, Jobs list
- the user switcher in the header and how it sets the identity header on every orval call
- the component library (or none)
- field-level validation errors from ProblemDetails
- presenting a 409 for concurrent decisions, stale positions and publications (showing the current state)
- polling a job's progress, the header "N jobs running" indicator
- the snapshot preview dialog ("Remove these votes")

Make a rough clickable prototype of the shell and one or two screens to react to. Needed before slice 1 in [Set the build order](12-build-order.md).
