# 03: The full API contract as 501 stubs

**What to build:** every endpoint of every slice exists with its final route, policy, request and response DTOs and problem types, and returns 501 ProblemDetails `not-implemented` until its slice fills it in. Each area (imports, conflicts, votes and timeline, positions, snapshots and publication) is its own feature module with its own service registration, already wired into startup, so a slice edits only its own module. The OpenAPI 3.1 document is written to `contracts/openapi.json` on every build and committed. The role-matrix test is generated from endpoint metadata.

**Blocked by:** 02

**Status:** done

- [x] Every ING, CON, HIS, POS and SNP endpoint (and the comparison extra) is in the OpenAPI document with typed request, response and problem bodies (`validation`, `no-votes-match`, `no-position`, `already-decided`, `stale-position`, `quarter-published`, `unknown-user`, `forbidden`, `not-implemented`)
- [x] Each stub returns 501 ProblemDetails with `traceId` to a caller with the right role
- [x] Building the API rewrites `contracts/openapi.json`; the committed copy matches
- [x] JSON number handling is strict, so numbers are never `number | string` in the client
- [x] The role-matrix test enumerates every endpoint from metadata: every endpoint as nobody gets 401, every admin endpoint as an analyst gets 403, and an endpoint without a policy fails the test
- [x] Validation error keys are camelCase JSON paths (tested on one stubbed request body)
