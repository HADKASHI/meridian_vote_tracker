# Slice 10: Interface

Status: ready-for-agent
Blocked by: Slices 4, 5 and 6
Runs in parallel with: slice 2
PR title: `Slice 10: Interface`
Requirements: the brief's Appendix B (example screens); map ticket 19 items 2, 11 and 12
User stories in the spec: none new; every screen of stories 1–118 gets its finished look

Read [the spec](../../spec.md) first, especially "Angular app", "Build order and slices", "Delivery" and "Testing Decisions". Then read [map ticket 19](../../../meridian-vote-tracker/issues/19-angular-app-structure.md) and open its prototype screenshots in `../../../meridian-vote-tracker/research/19-angular-prototype/`.

Numbered 10 because it was added after the plan, but it runs before slices 7 and 8, so the comparison page and the finishing pass build on the finished look.

## Goal

Every screen looks like a finished product, built from Angular Material, while behaving exactly as it does now. The slices so far built screens that work and are tested, but with almost no visual design: plain headings and lists, a 25-line global stylesheet, no layout, and the shared badges of map ticket 19 left unstyled. This slice gives the app one consistent look, using the brief's Appendix B and the prototype screenshots as the target.

## Lane

The web app only: the global styles and Material theme, the app shell in `core/`, the shared components in `shared/`, and the templates and styles of every feature folder. It changes no behaviour, no API call and nothing in the backend or the contract. Keep every `data-testid`, so the existing Vitest tests keep passing and are changed only where markup they assert on moves.

In scope:
- **Shell**: a Material toolbar with the app name, navigation showing the active route, the Review badge, the jobs indicator and the user switcher; one page container with a sensible maximum width and consistent spacing; page titles and breadcrumbs back to the list a page came from.
- **Shared components** (map ticket 19 item 2), built from Material and used everywhere their data appears: outcome chips (new, unchanged, duplicate, invalid, contradiction, conflict), job-state chips (queued, running, done, failed), the from → to display, the score with its basis, the problem banner and the already-decided card.
- **Every screen** uses Material tables, cards, lists, chips, progress bars and dialogs where they fit, with loading, empty and error states: imports list and import report (job progress, counts as filter chips, warnings, conflicts opened, row records); Review inbox split pane and decision pane; votes search, vote page and timeline; positions worklist and editor; snapshots list, new snapshot with its preview dialog, snapshot page with ranking, breakdown and publish panel; publications; jobs; not found.
- **Long content** wraps or collapses instead of widening the page: a warning naming 69 missing states shows a count and expands to the list.
- **Widths**: nothing scrolls sideways at 390 px; tables scroll inside their own container when they must.
- **Light and dark**: both follow the system setting through the Material theme's tokens, with no hard-coded colours.
- **Accessibility**: every action reachable by keyboard with a visible focus, every form field labelled, status never shown by colour alone (chips carry their word).
- Display conventions of map ticket 19 item 12 stay as they are: ISO dates, scores to 1 decimal with their basis, glossary words exactly.

## Data it arranges

Synthetic data only: the `seed-demo` command plus the committed CSV fixtures (an original file and a revision of it, run through the Imports page). Assignment data from `candidate_data/` never appears in a screenshot, a commit or the PR.

## Done when

- every screen listed under Lane has its finished look, in light and dark
- nothing on any screen scrolls sideways at 390 px or at 1280 px
- a Playwright script (development only, not part of CI; slice 8 reuses its setup for the smoke test) captures every screen at 1280 px and 390 px against `docker compose up` with synthetic data, and the screenshots are committed to `docs/screens/`
- the PR body shows a before and an after screenshot of each screen, linking the committed images
- the web lint, Vitest tests and build pass, and the backend is untouched
