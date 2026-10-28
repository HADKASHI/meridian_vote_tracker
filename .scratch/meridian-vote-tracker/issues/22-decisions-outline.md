# Outline DECISIONS.md

Type: grilling
Status: resolved
Blocked by: 12

## Question

DECISIONS.md must fit 2 pages, and today it is far longer. Decide:
- what stays in it, and what moves to linked notes or is dropped
- the order of its sections
- the plan for splitting the next phase across four developers
- which standards to set first
- how the build order and its reasoning are presented (the brief assesses the order)

Needed before the finishing pass in [Set the build order](12-build-order.md).

## Answer

Decided with the user in three rounds. The user reframed the four-developer plan: it describes how four people would build **this** system in parallel, through deployable seams, not a set of future features. It replaces my four product areas.

1. **What it must cover** (the user confirmed this reading of the brief):
   - the key decisions and trade-offs
   - the data-access explanation (section 9)
   - the four-developer plan
   - the standards to set first
   - the build order and why
2. **Budget**: about 1,000 words. "2 pages" is checked by printing the rendered Markdown to A4 PDF in the finishing pass. No code blocks, tables or diagrams.
3. **The full reasoning moves to six ADRs in `docs/adr/`**, about a page each, with context, decision, rejected alternatives and consequences:
   1. typed append-only revisions with current pointers, and the guard
   2. import classification and conflicts
   3. the job table as the queue, and Blob for uploaded files
   4. snapshots store their answer, and publication is a chain
   5. EF Core only, with rules in C# and facts from SQL
   6. the scoring rules

   `research/16-schema.md` becomes `docs/schema.md`. DECISIONS.md and the ADRs never link into `.scratch/` (whether it ships is [Decide what AI tooling ships with the submission](23-shipped-ai-tooling.md)).
4. **Outline**:
   1. What this is and how to read it (~60 words)
   2. Key decisions (~450): data model and history, import rules, concurrency and roles, scoring rules, snapshots and publication, background jobs and files, data access. 2–4 bullets each, in the form "decision, why, what was rejected", linking its ADR.
   3. Build order and why (~100)
   4. Quality: testing and observability, the guarantees only (~80)
   5. Not done and known limitations, written last (~100)
   6. Four developers (~150)
   7. Standards to set first (~80)
5. **Four developers**: build the same system in parallel, through deployable seams:
   - **One shared skeleton first**: compose, the whole schema as migration 1, auth, the **full OpenAPI contract with every endpoint returning 501 "not implemented"**, the generated client, and the Angular shell with every route. It's deployable on day one.
   - **Four owners of vertical slices**, each story replacing some 501s end to end and shipping on its own: **A** import and import at scale; **B** conflicts and the Review inbox; **C** vote page, timeline, positions and weights; **D** snapshots and publication.
   - **Nobody waits**: the schema and contract are fixed up front, and each developer seeds the data their slice needs through committed fixtures (B builds conflict review before A's importer exists).
6. **Standards to set first**:
   1. **Contract first**: the API changes through the committed OpenAPI file; the generated client is never hand-edited.
   2. **Migrations**: one migration per PR; guarded tables only get columns added; every new table is guarded unless allowlisted, enforced by the guard test.
   3. **Definition of done**: a slice ships API, screen and tests together, and the guard, role-matrix and race tests stay green.
   4. **Error contract**: a catalogue of ProblemDetails types (validation, already decided, stale version, not implemented, …) that the UI maps one-to-one.
   5. **Data handling**: client data never goes in git, logs or fixtures, and the glossary's terms are used in code, the API and the UI.
7. **Timing**: the six ADRs are created from today's DECISIONS.md text at the start of the build. From then on each slice adds its reasoning to an ADR, not to DECISIONS.md. DECISIONS.md is rewritten to this outline in the finishing pass, once "not done" is known.
