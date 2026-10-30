# 05: Rewrite DECISIONS.md to two pages

**What to build:** DECISIONS.md rewritten to the outline of map ticket 22, about 1,000 words with no code, tables or diagrams: what this is and how to read it; key decisions (data model and history, import rules, concurrency and roles, scoring rules, snapshots and publication, background jobs and files, data access), each "decision, why, what was rejected" with its ADR linked; build order and why; quality guarantees; not done and known limitations; the four-developer plan on the 501-stubbed skeleton; and the standards to set first. Written last, so "not done" is accurate.

**Blocked by:** 01, 02, 03, 04

**Status:** done

- [x] It follows the outline, links its ADRs and the schema document, and never links into `.scratch/`
- [x] It is about 1,000 words, and the rendered Markdown printed to A4 PDF fits 2 pages
- [x] Every claim matches the code and the ADRs as merged

## Comments

- Printed to A4 with Chromium from the Markdown rendered by marked: 2 pages in a plain print (1cm margins, browser default or 11pt sans-serif) and with GitHub's stylesheet at 10pt with 1.5cm margins; 3 pages with GitHub's stylesheet at 11pt or more. The user chose to keep the text at about 760 words, below the outline's ~1,000, rather than cut further or grow it.
- Follow-up at the user's request: added the evidence metrics and confidence band, snapshot drift and comparison, the Azure resources used and rejected, and how PDF extraction quality would be measured (brief section 11). Trimmed elsewhere to about 800 words; it still prints to 2 A4 pages under the settings above.
