# Decide correlation IDs and logging

Type: grilling
Status: resolved
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

## Answer

Decided with the user in one grilling round.

1. **W3C trace context, started by the server**: .NET's built-in `Activity` / OpenTelemetry tracing. ASP.NET Core starts a trace for each request, or continues one from an incoming `traceparent`, which nginx passes through. The browser starts no traces: no OpenTelemetry in Angular, and the free interceptor slot stays free.
2. **A job continues the trace of the request that created it**: `job` gets `trace_parent text`, captured when the job is queued. The worker runs the job as a child span of that trace (same trace id), and each retry is its own span. Searching one trace id finds the upload, the parse, every vote transaction and any failure. Rejected: a new trace with a span link (purer OpenTelemetry for async work, but harder to search in plain logs). The change is to `job` only, the mutable table, so the guarded schema is untouched.
3. **Built-in JSON console logging, no Serilog**: `AddJsonConsole` with scopes on, so every line carries `TraceId` and `SpanId`. Scopes: `UserId` on API requests; `JobId`, `ImportId` or `SnapshotId`, and the current `VoteId` in the worker. JSON in compose (switched by environment variables, per [Design Docker Compose and image builds](18-docker-compose.md)); the readable format under `dotnet run`.
4. **Application Insights only through configuration**: the Azure Monitor OpenTelemetry distro (`Azure.Monitor.OpenTelemetry.AspNetCore`) switches on only when `APPLICATIONINSIGHTS_CONNECTION_STRING` is set; otherwise console only. No local trace dashboard: that would be a third extra.
5. **Logs never carry client data**: ids, counts, outcomes and timings only. Never raw CSV text, titles, ballots, comments or file contents. Levels:
   - Information: business events (import queued, started and finished with outcome counts; conflict decided; snapshot calculated and published) and expected 409s
   - Warning: a heartbeat-sweep requeue
   - Error: a job failure or an unexpected 500
6. **Errors carry their trace id to the user**: every ProblemDetails, including custom 400s and 409s, includes `traceId`. The UI shows it as "Reference: …" (as [Shape the Angular app](19-angular-app-structure.md) assumed). A failed job keeps `last_error` (message only, no data) and its trace id, both shown on the import or snapshot page.
7. **No custom metrics**: progress, durations and counts already live in `job` rows and the import report, and the 1M-row test reports time and memory. When Application Insights is on, it collects the built-in ASP.NET Core and HttpClient metrics.
8. **AI calls (PDF bonus)** go through an instrumented `HttpClient` inside the job's trace. Their log line holds model, latency, token counts and outcome, never document content or prompts.
