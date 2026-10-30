# 05: Rewrite DECISIONS.md to two pages

**What to build:** DECISIONS.md rewritten to the outline of map ticket 22, about 1,000 words with no code, tables or diagrams: what this is and how to read it; key decisions (data model and history, import rules, concurrency and roles, scoring rules, snapshots and publication, background jobs and files, data access), each "decision, why, what was rejected" with its ADR linked; build order and why; quality guarantees; not done and known limitations; the four-developer plan on the 501-stubbed skeleton; and the standards to set first. Written last, so "not done" is accurate.

**Blocked by:** 01, 02, 03, 04

**Status:** ready-for-agent

- [ ] It follows the outline, links its ADRs and the schema document, and never links into `.scratch/`
- [ ] It is about 1,000 words, and the rendered Markdown printed to A4 PDF fits 2 pages
- [ ] Every claim matches the code and the ADRs as merged
