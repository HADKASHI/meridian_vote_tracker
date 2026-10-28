# Research: current versions and tooling

Ticket: `issues/02-versions-and-tooling.md`
Researched: 2026-10-26. Every claim cites the primary source it was read from on that date.

Access notes: `dot.net`/`builds.dotnet.microsoft.com`, `learn.microsoft.com`, `angular.dev` and `postgresql.org` returned proxy 403. The same content was read from the source repos behind those sites (via `raw.githubusercontent.com`): `dotnet/core` release metadata, `dotnet/AspNetCore.Docs` (the source of learn.microsoft.com ASP.NET Core pages), `angular/angular` `adev/` (the source of angular.dev). Package facts come from `api.nuget.org`, `registry.npmjs.org`, `mcr.microsoft.com` and `hub.docker.com` / `docker-library`. No secondary sources (endoflife.date etc.) were used.

## 1. .NET

| Channel | Type | Phase | Latest | End of support |
|---|---|---|---|---|
| 11.0 | STS | go-live (RC) | 11.0.0-rc.3 (2026-10-25) | not yet published |
| **10.0** | **LTS** | **active** | **10.0.14, SDK 10.0.403 (2026-10-25)** | **2028-11-14** |
| 9.0 | STS | maintenance | 9.0.22 | 2026-11-10 |
| 8.0 | LTS | maintenance | 8.0.33 | 2026-11-10 |

Source: `https://raw.githubusercontent.com/dotnet/core/main/release-notes/releases-index.json` and `.../release-notes/11.0/releases.json`.

- .NET 11 exists only as a release candidate (`support-phase: go-live`, `release-type: sts`); no GA or EOL date is in the metadata yet. Not verified: the planned GA date (dotnet.microsoft.com blocked).
- .NET 8 and 9 both reach end of support on 2026-11-10, two weeks from today, so .NET 10 is the only sensible target.
- Container images exist: `mcr.microsoft.com/dotnet/aspnet:10.0` (and `10.0.14`, `10.0-alpine`) and `mcr.microsoft.com/dotnet/sdk:10.0` (`10.0.403`). Source: `https://mcr.microsoft.com/v2/dotnet/aspnet/tags/list`, `.../sdk/tags/list`.

## 2. Angular

- Latest stable: `@angular/core` 22.2.3, `@angular/cli` 22.2.4; `next` is 22.3.0-next.2. v22.0.0 was released 2026-06-03. Source: `https://registry.npmjs.org/@angular/core`, `.../@angular/cli/latest`.
- Support table (source: `angular/angular` `adev/src/content/reference/releases.md`):

  | Version | Status | Released | Active ends | LTS ends |
  |---|---|---|---|---|
  | ^22.0.0 | Active | 2026-06-03 | 2027-06 | 2028-06 |
  | ^21.0.0 | LTS | 2025-11-19 | 2026-06-03 | 2027-06 |
  | ^20.0.0 | LTS | 2025-05-28 | 2025-11-19 | 2026-11-28 |

- Cadence changed: "A major release every 12 months ... Until Angular v22, Angular had a 6-month major release cycle". v23.0 is scheduled "~ June 2027"; v22.3 ~November 2026 (same file). So v22 stays current for the life of this assignment.
- Toolchain for 22.0.x: Node `^22.22.3 || ^24.15.0 || ^26.0.0`, TypeScript `>=6.0.0 <6.1.0`, RxJS `^6.5.3 || ^7.4.0`. Source: `adev/src/content/reference/versions.md`.
- Standalone: default since v19.0.0 ("Angular directives, components and pipes are now standalone by default"). Source: `angular/angular` `CHANGELOG.md`, 19.0.0 breaking changes.
- Zoneless: promoted to stable in 20.2.0 (commit 141bb75, "Promote zoneless to stable"); "Zoneless is the default in Angular v21+". Sources: `CHANGELOG.md` 20.2.0; `adev/src/content/guide/zoneless.md`.
- Signals: signal-based APIs are mainstream; Signal Forms were graduated to public API during v22 (changelog: "graduate signal forms APIs to public API"); `httpResource` is under active development (multiple fixes in 22.x). Source: `CHANGELOG.md`.
- v22 breaking changes relevant to us (source: `CHANGELOG.md` 22.0.0): components without an explicit `changeDetection` are now `OnPush` by default; `withFetch` is deprecated (fetch is the default `HttpClient` backend; use `withXhr` to keep upload progress); TypeScript < 6.0 dropped.

## 3. OpenAPI document generation: Microsoft.AspNetCore.OpenApi vs Swashbuckle

### Microsoft.AspNetCore.OpenApi (built in)

- Latest stable 10.0.14 (published 2026-10-25); 11.0.0-rc.3 also exists. Source: `api.nuget.org` flat container / registration for `microsoft.aspnetcore.openapi`.
- First-party since .NET 9; the Web API template calls `AddOpenApi()` / `MapOpenApi()`. It generates documents only; Swagger UI or Scalar must be added separately. Sources: `dotnet/AspNetCore.Docs` `aspnetcore/fundamentals/openapi/overview.md`, `.../aspnetcore-openapi.md` ("Generate OpenAPI documents at build time" note).
- Spec version: .NET 10 emits **OpenAPI 3.1 by default**, configurable with `options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_0`. **.NET 11 changes the default to 3.2.** Sources: `aspnetcore-openapi.md` lines "Support for generating OpenAPI version 3.1 documents starting with .NET 10, and OpenAPI version 3.2 ... starting with .NET 11" and "Starting in .NET 11, the default ... is 3.2. In .NET 10, the default is 3.1"; `release-notes/aspnetcore-10/includes/openApi.md`.
- .NET 10 moved to Microsoft.OpenApi 2.x (breaking for transformers), added YAML output, `oneOf`/type-array nullability, and descriptions as siblings of `$ref`. Source: `release-notes/aspnetcore-10/includes/{openApi,upgrade-microsoft-openapi-2,openapi-schema-enhancements}.md`.
- Build-time generation: add `Microsoft.Extensions.ApiDescription.Server` (10.0.x) and set `<OpenApiGenerateDocumentsOnBuild>true</OpenApiGenerateDocumentsOnBuild>`; `OpenApiDocumentsDirectory` controls the output folder and `OpenApiGenerateDocumentsOptions` can pass `--openapi-version`, `--file-name`, `--document-name`. This lets `dotnet build` write the contract file that the Angular generator reads, with no running server. Sources: `overview.md`, `aspnetcore-openapi.md`.
- ProblemDetails: `ProducesProblem` / `ProducesValidationProblem` (Minimal APIs) and `[ProducesResponseType<ProblemDetails>(400, "application/problem+json")]` (controllers) add the schema; in controllers "the schema for the response body of 4xx responses is inferred to be a problem details object". The runtime side is `AddProblemDetails()` + `UseExceptionHandler()` / `IProblemDetailsService`. Sources: `aspnetcore/fundamentals/openapi/include-metadata.md`; `aspnetcore/fundamentals/error-handling.md` ("Problem details").
- **Pitfall for client generators**: with the ASP.NET Core web default `JsonNumberHandling.AllowReadingFromString`, numeric types are emitted as `type: [integer, string]` plus a `pattern`. With `JsonNumberHandling.Strict` they are plain `integer` / `number`. Source: `include-metadata.md` (numeric type tables); `release-notes/aspnetcore-10/includes/openApi.md` line 16. Verified locally: both orval 8.45.0 and ng-openapi-gen 1.1.0 turn such a property into `id: number | string`. **Set `NumberHandling = Strict` on the HTTP JSON options** so the generated TypeScript has `number`.

### Swashbuckle.AspNetCore

- Latest 10.3.1 (published 2026-10-19), so it is maintained. Source: `api.nuget.org` registration for `swashbuckle.aspnetcore`.
- v10 moved to Microsoft.OpenApi 2.x "to add support for generating OpenAPI 3.1 documents"; its support matrix lists ASP.NET Core >= 8.0 with "3.1, **3.0**, 2.0" (bold = default, so 3.1 is opt-in). Source: `domaindrivendev/Swashbuckle.AspNetCore` `README.md`.
- It is no longer in the Microsoft templates, which use `Microsoft.AspNetCore.OpenApi` (`overview.md`). Not verified: the original dotnet/aspnetcore announcement of the removal (GitHub issue pages not reachable).
- ProblemDetails: works through the same `[ProducesResponseType]` / `Produces*` metadata (both read ApiExplorer). Its advantage is maturity (filters, Swagger UI bundled). Its drawback for us is a second, third-party dependency that does what the framework now does.

**Verdict**: use `Microsoft.AspNetCore.OpenApi` 10.0.x with build-time generation. Keep the 3.1 default (both candidate generators below read 3.1). Add Scalar (`Scalar.AspNetCore`, latest 2.18.3 per `api.nuget.org`) or Swagger UI for the interactive page. Pin the version explicitly so that a later move to .NET 11 (default 3.2) does not silently change the contract format.

## 4. Typed Angular client generators

| Tool | Latest / last publish | OpenAPI 3.1 | Angular output | Runtime needed | Notes |
|---|---|---|---|---|---|
| **orval** | 8.45.0, 2026-10-25 | Docs say "any valid OpenAPI v3 or Swagger v2"; `@orval/core` handles `3.1` documents; verified with a 3.1 type-array spec | `@Injectable({providedIn:'root'})` services using `inject(HttpClient)`; optional signal-first `httpResource` functions (`override.angular.retrievalClient: httpClient \| httpResource \| both`); optional Zod runtime validation | Node >= 22.18 | MIT; very active; config in `orval.config.ts` |
| **ng-openapi-gen** | 1.1.0, 2026-09-23 | "An OpenAPI 3.0 and 3.1 code generator for Angular"; 3.1 since 1.0 | Per-operation functions + injectable `Api` service (or per-tag services); docs use `inject()` and signals; peer `@angular/core >=16` | Node | MIT; releases are infrequent (1.0.5 in Nov 2025, then 1.1.0) |
| openapi-generator `typescript-angular` | npm wrapper 2.42.0 (2026-10-20); generator 7.26.0 (2026-10-06) | "3.1 (beta support)" | Option `ngVersion` (default 22.0.0), `withHttpResource` (Angular 20+), `providedIn` | **Java 11+** (the npm package downloads and runs a JAR) or Docker | Apache-2.0; most generators but heavyweight |
| NSwag (`nswag` npm / `NSwag.MSBuild`) | 14.7.1, 2026-04-20 | README: "Swagger/OpenAPI 2.0 and 3.0 toolchain"; no 3.1 claim | Angular class-based clients | **.NET runtime** | Last release six months ago; would force `OpenApi3_0` output |
| @hey-api/openapi-ts | 0.99.0 (pre-1.0) | not checked in depth | Angular plugin | Node | Pre-1.0 API; not considered further |

Sources: `registry.npmjs.org/{orval,@orval/core,@orval/angular,ng-openapi-gen,@openapitools/openapi-generator-cli,nswag,@hey-api/openapi-ts}`; `api.nuget.org` (`nswag.msbuild`); READMEs at `orval-labs/orval` (`README.md`, `docs/content/docs/index.mdx`, `docs/content/docs/guides/angular.mdx`), `cyclosproject/ng-openapi-gen/README.md`, `OpenAPITools/openapi-generator` (`README.md` compatibility table, `docs/generators/typescript-angular.md`), `RicoSuter/NSwag/README.md`.

Local check (scratch only, not committed): given a 3.1 spec as .NET 10 emits it (type arrays with `null`, `application/problem+json` response), both `npx orval` (client `angular`) and `npx ng-openapi-gen` ran with no Java/.NET and produced correct nullable types (`string | null`) and a typed `ProblemDetails` interface. orval produced an `inject(HttpClient)` service plus `httpResource` helpers.

## 5. PostgreSQL and Npgsql

- `Npgsql.EntityFrameworkCore.PostgreSQL` latest stable **10.0.3** (published 2026-07-10), depends on `Microsoft.EntityFrameworkCore` `[10.0.4, 11.0.0)` and `Npgsql >= 10.0.3`, target `net10.0`. 11.0.0-rc.2 also exists. Source: `api.nuget.org` registration and catalog entry for 10.0.3. EF Core itself is at 10.0.14.
- PostgreSQL: current stable major is **18** (18.6); 19 is at RC (`19rc1`). Official image tags: `postgres:18.6`, `postgres:18`, `latest`, `18-alpine` (Debian trixie default; bookworm and alpine3.23/3.24 variants). Source: `docker-library/official-images` `library/postgres`; `docker-library/postgres` `versions.json`. Not verified: PostgreSQL 19 GA date and per-major EOL dates (postgresql.org blocked).
- **Compose pitfall**: from 18 the image sets `PGDATA=/var/lib/postgresql/18/docker` and `VOLUME /var/lib/postgresql` (previously `/var/lib/postgresql/data`). Mount the named volume at `/var/lib/postgresql`. Source: `docker-library/postgres` `18/trixie/Dockerfile` (comment and `ENV PGDATA`).

## Recommendation

- **Backend**: .NET 10 LTS (SDK 10.0.403, runtime 10.0.14, support until 2028-11-14). Images `mcr.microsoft.com/dotnet/sdk:10.0` / `aspnet:10.0`. Do not use 11 (RC, STS) or 8/9 (end of support 2026-11-10).
- **OpenAPI**: `Microsoft.AspNetCore.OpenApi` 10.0.x and `Microsoft.Extensions.ApiDescription.Server` to write `openapi.json` on build; OpenAPI 3.1; `AddProblemDetails()` with `Produces[Validation]Problem` metadata so every error response is typed `application/problem+json`; JSON `NumberHandling = Strict`. Scalar for the UI. No Swashbuckle.
- **Client generator**: **orval** (8.45.x, run as an npm script, e.g. `"gen:api": "orval"`, from the committed or build-produced `openapi.json`). It is actively maintained, reads 3.1, needs no Java or .NET, and emits `inject(HttpClient)` services plus optional `httpResource` signal functions. Fallback: ng-openapi-gen 1.1.0 (also Node-only and 3.1-capable, but releases rarely). Not NSwag (3.0 only, needs .NET) or openapi-generator (Java; 3.1 beta).
- **Frontend**: Angular 22.2.x (Active until 2027-06, LTS to 2028-06), standalone, zoneless (the default), signals, `OnPush` (the default); Node 24 LTS (`^24.15.0`), TypeScript 6.0.
- **Database**: PostgreSQL 18 (`postgres:18` or pin `postgres:18.6`, volume at `/var/lib/postgresql`) with `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3.
