# 03: Shared status components

**What to build:** the shared components of map ticket 19 item 2, built from Material and ready for every screen: outcome chips (new, unchanged, duplicate, invalid, contradiction, conflict), job-state chips (queued, running, done, failed), the from → to display, the score with its basis, the problem banner, the already-decided card, a list that shows a count and expands to its items when long, and loading, empty and error states. Each chip carries its glossary word, so status never depends on colour alone.

**Blocked by:** 02

**Status:** ready-for-agent

- [ ] Outcome chip and job-state chip show the glossary word exactly, with a distinct theme-token tone per value
- [ ] From → to shows both values with an arrow that reads as "to" for screen readers
- [ ] Score with basis shows the score to 1 decimal and its basis, n/a when nothing is countable
- [ ] Problem banner and already-decided card restyled from Material tokens, with their trace reference
- [ ] A long list (e.g. 69 missing states) shows its count and expands to the items by keyboard or click; a short one shows inline
- [ ] Loading, empty and error states share one look
- [ ] Vitest proves the words shown, the score format and the expand; lint and build pass
