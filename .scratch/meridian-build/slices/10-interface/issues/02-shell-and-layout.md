# 02: Shell, page layout and theme

**What to build:** the app shell looks finished: a Material toolbar with the app name, navigation that shows the active route, the Review badge, the jobs indicator (with a spinner while jobs run) and the user switcher showing each user's role; on narrow screens the navigation collapses into a menu. Every page sits in one container with a sensible maximum width and consistent spacing, and opens with a page title and, on detail pages, a breadcrumb back to the list it came from. Light and dark follow the system setting through the Material theme's tokens, every action has a visible keyboard focus, and the Jobs and Not found pages get their finished look.

**Blocked by:** 01

**Status:** ready-for-agent

- [ ] Toolbar with app name, navigation with the active route marked (not by colour alone), Review badge, jobs indicator and user switcher
- [ ] Below a phone width the navigation is reachable from a menu button and nothing in the toolbar scrolls sideways
- [ ] A shared page header (title, optional breadcrumb, optional actions) used by every page
- [ ] One page container with a maximum width; no hard-coded colours, only theme tokens; light and dark follow the system
- [ ] A visible focus ring on every link and button
- [ ] Jobs page: table in a card that scrolls inside its own container, job-state shown in words, progress bar while running, empty state
- [ ] Not found page: a finished empty state with a way back
- [ ] Existing `data-testid`s kept; lint, Vitest and build pass
