# Decide concurrency and roles

Type: grilling
Status: resolved

## Question

How do we guarantee that only one of two simultaneous conflict decisions takes effect, with a clear message and the current state shown to the loser (CON-05)? How do users identify themselves (pre-seeded Dana, Omar, Lena with a user switcher) and how are roles enforced in the API, not only in the UI (section 3, POS-01, SNP-05)? Cover optimistic concurrency vs conditional updates, the error contract for a lost race (409 shape), and admin-only actions.

## Answer

Decided with the user in one grilling round, plus a follow-up on Q7.

1. **Identity**: a small custom ASP.NET authentication handler turns the header user switcher (Dana, Omar, Lena) into a standard signed-in user with role claims. Endpoints use ordinary policy attributes; production auth (Entra ID or JWT) would replace only the handler.
2. **Roles**: two policies. `Analyst` (both roles) may view, upload, decide conflicts and create snapshots; `Admin` (Lena) may also set positions and weights and publish snapshots. Admin is a superset.
3. **Refusals**: 401 for an unknown or missing user, 403 for the wrong role, both as ProblemDetails. An integration test calls every write endpoint as each role.
4. **Two analysts, one conflict (CON-05)** _(mechanism refined by [Choose the history storage pattern](15-choose-history-pattern.md): a unique, insert-only decision record replaces the status update; same guarantee)_: a single conditional update ("only if still pending") in the same transaction that moves the approved value and records the decision. The loser gets 409 with an "already decided" error type and the current state (who, when, what, comment) in the body; the attempt is recorded as "not applied". A test fires two decisions in parallel and expects one 200 and one 409.
5. **Positions**: optimistic concurrency with a version; a stale save gets 409 plus the current value.
6. **Imports are applied one at a time** in upload order by a single worker; uploads are still accepted instantly. Safety net: a unique constraint allowing at most one pending conflict per subject and incoming value (ING-12). How the queue works is [Design background jobs and live progress](10-background-jobs.md).
7. **Per-vote consistency**: an import applies one vote per transaction and locks that vote; a conflict decision locks the same vote. This makes "read the approved value, write the verdict" one step, preventing both a bogus conflict and a missed conflict (a later file disagreeing with a just-decided value without a conflict opening).
