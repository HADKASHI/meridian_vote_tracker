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

## Comments

- From [Design Docker Compose and image builds](18-docker-compose.md): nginx passes `traceparent` through and sets `X-Forwarded-For` and `X-Forwarded-Proto`. Every container logs to stdout, so JSON console logging can be switched on with environment variables in compose.
- From [Shape the Angular app](19-angular-app-structure.md): the app sets no correlation header today; an interceptor slot is free if the browser should start the trace. Error notices show the ProblemDetails `traceId`.
