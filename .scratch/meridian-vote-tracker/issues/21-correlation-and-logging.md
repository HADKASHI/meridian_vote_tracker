# Decide correlation IDs and logging

Type: grilling
Status: open
Blocked by: 12

## Question

How does one user action stay traceable from the Angular request through the API, into the job row and the worker, and on to any AI call (the PDF bonus)? Decide:
- the ID: W3C `traceparent` / OpenTelemetry vs a custom header
- carrying it on the `job` row
- logging shape: structured JSON to the console
- whether Application Insights is wired in as an optional, configuration-only Azure resource or left as local logging only
- what an error shown to the user includes (a ProblemDetails `traceId`)

Needed before the walking skeleton in [Set the build order](12-build-order.md).
