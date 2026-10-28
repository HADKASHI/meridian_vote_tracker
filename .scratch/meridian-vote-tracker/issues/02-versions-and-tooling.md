# Choose current versions and tooling

Type: research
Status: resolved

## Question

As of late 2026, from primary sources: which .NET LTS and Angular versions to target; whether to use ASP.NET Core's built-in OpenAPI document generation (Microsoft.AspNetCore.OpenApi) or Swashbuckle; and which generator produces the typed Angular client from the OpenAPI contract (NSwag, openapi-generator typescript-angular, orval, or other). The client must be generated, not hand-written (brief 7.7). Also: the current EF Core provider for PostgreSQL (Npgsql) and the PostgreSQL image version.

## Answer

Detail and sources: [research/02-versions-and-tooling.md](../research/02-versions-and-tooling.md).

- **.NET 10 LTS** (SDK 10.0.4xx, supported to 2028-11). .NET 8 and 9 both lose support on 2026-11-10, so targeting them would be a red flag.
- **Built-in `Microsoft.AspNetCore.OpenApi`**, not Swashbuckle. `openapi.json` is emitted at build time via `Microsoft.Extensions.ApiDescription.Server`; pin the OpenAPI version explicitly (3.1). Errors use ProblemDetails. JSON `NumberHandling = Strict`, or numbers become `number | string` in the client.
- **orval** generates the typed Angular client (npm-only; OpenAPI 3.1; `inject(HttpClient)`). Fallback: ng-openapi-gen. NSwag and openapi-generator rejected (OpenAPI 3.0 only / Java dependency).
- **Angular 22** (standalone, zoneless and OnPush by default), Node 24, TypeScript 6.0.
- **PostgreSQL 18** (`postgres:18`; volume at `/var/lib/postgresql`) with Npgsql EF Core provider 10.0.x.
