# Angular app structure: supporting note

Supports [Shape the Angular app](../issues/19-angular-app-structure.md). The decisions themselves are in the ticket's Answer; this note keeps the evidence, the prototype verdict and the sketches the slices will copy.

## Facts checked (2026-10-28)

Read from the npm registry tarballs themselves (angular.dev, unpkg and the GitHub API were blocked by the proxy, as in [02](02-versions-and-tooling.md)).

| Fact | Evidence |
|---|---|
| `@angular/core` 22.2.3, `@angular/material` and `@angular/cdk` 22.2.4 (peer `@angular/core ^22 \|\| ^23`) | `registry.npmjs.org/<pkg>/latest` |
| `resource`, `rxResource` and `httpResource` are stable public API (`@publicApi 20.0` / `22.0`, no `@experimental`) | `@angular/core@22.2.3` `types/core.d.ts`, `rxjs-interop.d.ts`; `@angular/common@22.2.3` `types/http.d.ts` line ~2534 |
| Signal Forms are public API in 22 (`form`, `submit`, `validateHttp`, all `@publicApi 22.0`); `submit(form, { action })` lets the action return server errors bound to a field (`{ fieldTree: f.name, kind: 'server', message }`) | `@angular/forms@22.2.3` `types/_structure-chunk.d.ts` lines 2060–2104 |
| Angular Material 22 form controls know Signal Forms (`input`, `select`, `checkbox`, `datepicker` reference the signal-forms field binding) and ship component test harnesses (`*-testing`) | `@angular/material@22.2.4` `fesm2022/`, `types/` |
| The CLI `unit-test` builder offers `karma` or `vitest`, default **vitest** | `@angular/build@22.2.4` `src/builders/unit-test/schema.json` |
| Zoneless is the default since v21; standalone since v19; OnPush default in 22 | [02 research](02-versions-and-tooling.md) §2 |
| orval 8.45.0 Angular output: `inject(HttpClient)` services, optional `httpResource` functions (`override.angular.retrievalClient: 'httpClient' \| 'httpResource' \| 'both'`), `HttpContext` and `observe: 'events'` options passed through | `@orval/angular@8.45.0` `dist/index.mjs` |

Consequence: both orval outputs go through Angular's `HttpClient`, so one functional interceptor registered with `provideHttpClient(withInterceptors([...]))` sees every generated call, `httpResource` reads included. No orval mutator is needed.

## Prototype verdict

[19-angular-prototype/prototype.html](19-angular-prototype/prototype.html) (static, throwaway; open in a browser). Screenshots in the same folder:

| Screenshot | Shows |
|---|---|
| `01-review-split-pane.png` | Variant A: conflict list left, decision pane right (both sides with their origins, comment, Accept / Keep) |
| `02-review-grouped.png` | Variant B: conflicts grouped by vote, one-click decide inline |
| `03-review-409-already-decided.png` | The loser's view after a 409: the winner's decision replaces the buttons |
| `04-user-switcher.png` | Header user switcher with roles; per-tab identity |
| `05-import-job-running.png`, `06-import-report.png` | Import page while its job runs, then the report (counts per outcome, conflicts opened, warnings, row records) |
| `07-snapshot-preview-blocked.png`, `08-snapshot-preview-removed.png` | Preview dialog blocked by votes without a position; after "Remove these votes" |
| `09-jobs-list.png` | Jobs list (running, queued with place in queue, done, failed with the reason) |

**Variant A wins.** A conflict decision is a judgement on evidence: B makes deciding fast but hides the origins of both values and the comment box, and invites click-through of rev2's 8 decisions. From B we keep one thing: list rows of the same vote stay adjacent (sort by vote, then subject), so a vote's conflicts read together. The 409 card (screenshot 03) reads well as a persistent replacement for the buttons; a toast would vanish before the analyst understands that their decision was not applied.

## Sketches the slices copy

```ts
// core/identity/identity.interceptor.ts
export const identityInterceptor: HttpInterceptorFn = (req, next) => {
  const user = inject(CurrentUser).id();             // signal
  return req.url.startsWith('/api/')
    ? next(req.clone({ setHeaders: { 'X-User-Id': user } }))
    : next(req);
};

// app.config.ts
provideHttpClient(withFetch(), withInterceptors([identityInterceptor, errorNoticeInterceptor]))
```

```ts
// core/identity/current-user.ts  (per tab, survives reload; new tabs start as the last choice)
read:  sessionStorage['mvt.user'] ?? localStorage['mvt.lastUser'] ?? first seeded user   // each access in try/catch
write: both keys on switch
```

```ts
// shared/http/poll.ts: reload a resource every `ms` while `active()` and the tab is visible
export function pollWhile(res: { status: Signal<ResourceStatus>; reload(): boolean }, ms: number, active: () => boolean) {
  const visible = inject(PageVisibility).visible;
  effect(onCleanup => {
    if (res.status() !== 'resolved' || !active() || !visible()) return;
    const t = setTimeout(() => res.reload(), ms);     // chained, never overlapping
    onCleanup(() => clearTimeout(t));
  });
}
```

```ts
// shared/problem/to-field-errors.ts
// ValidationProblemDetails.errors { "selection.from": ["..."] } -> Signal Forms server errors
submit(form, { action: async () => {
  try { await firstValueFrom(api.createSnapshot(form().value())); }
  catch (e) { return problemToFieldErrors(toProblem(e), form); }   // unknown keys -> form-level error shown in a banner
}});
```

Problem types (C# constants, mirrored as a TS union; 409 and 400 bodies are typed schemas in the OpenAPI document):

| type | status | extension body | Screen |
|---|---|---|---|
| `validation` | 400 | `errors` | field errors |
| `no-votes-match` | 400 | | snapshot form |
| `no-position` | 400 | `votes[]` | preview dialog's blocking list |
| `already-decided` | 409 | `decision` (who, when, accept/reject, value, comment) | Review decision pane |
| `stale-position` | 409 | `current` (stance, weight, by, at, revision id) | Positions row / vote page |
| `quarter-published` | 409 | `publication` (snapshot, by, at) | Publish dialog |
| `unknown-user` / `forbidden` | 401 / 403 | | snackbar; 401 opens the switcher |

Every problem carries ASP.NET's `traceId` extension; error notices show it (ticket 21).
