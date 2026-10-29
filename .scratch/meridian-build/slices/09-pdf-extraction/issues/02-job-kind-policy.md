# 02: Give job kinds one policy

**What to build:** Import and snapshot jobs behave exactly as before, but the per-kind rules the job processor keeps in separate branches (how many may run at once, the timeout, the log scope id) are read from one small policy, so a third kind is one entry and not a third copy. No behaviour changes; this is the "make the change easy" step.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

**Requirements:** none (prefactor)

- [ ] The claim, the timeout and the log scope all read the same per-kind policy
- [ ] Existing job tests (claim order, import cap, snapshot alongside import, timeout, sweeper) pass unchanged
- [ ] Adding a kind needs no new branch in any of the three places, shown by a test with a test-only kind
- [ ] Commit says it is a refactor of Meridian.Platform and is flagged for the PR
