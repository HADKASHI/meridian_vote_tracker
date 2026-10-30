# AI usage

## Tools and workflow

The project was built with Claude Code, mostly in cloud sessions, driven by the skills that ship in `.claude/skills/`. The AI proposes options with a recommendation, and I decide. Every step leaves a file in `.scratch/`, so the trail can be read after the fact.

1. **Design map** (`.scratch/meridian-vote-tracker/`): **wayfinder** kept a map of 23 decision tickets. Each was resolved by a **grilling** interview, a **research** investigation against primary sources (in `research/`, with the data audit scripts), or a **prototype** (the Angular app structure). **domain-modeling** and **grill-with-docs** kept `GLOSSARY.md` and the ADRs in step with the answers.
2. **Spec** (`.scratch/meridian-build/spec.md`): **to-spec** turned the resolved map into user stories, implementation and testing decisions, and a build order cut into slices.
3. **Slices** (`.scratch/meridian-build/slices/`): **build-slice** took one slice per session to one pull request. **to-tickets** broke the brief into tickets, which I approved before any code was written. Each ticket was built with **tdd** at the seams the spec had fixed, checked with **code-review** against the spec and the repo's standards, and described with **pr**. **triage** and **setup-matt-pocock-skills** set up the local issue tracker these skills share. **codebase-design** is the vocabulary that **tdd** consults.
4. **Merging**: I merged every pull request myself, with a merge commit, so each slice's commits land on `main` as written.

The skills were trimmed at the end to the ones this project used. `.claude/sandbox/` and `.claude/cloud-dev-setup.sh` only make container builds work behind the cloud session's proxy. The app doesn't use them.

## Where AI output was checked, rejected or corrected

### Design

- **Data audit:** the AI catalogued 25 defect classes in the candidate files. Its headline counts (vote 040 missing from rev2, vote 031 truncated, the slash dates, the 5-field row) were re-counted independently before being relied on. It quoted data rows in its report; these were removed, so the client's data stays out of git.
- **Keeping uploaded files:** the AI first proposed copying a row's raw text only for rejected or flagged rows. Questioning what the system must answer at any moment led to a simpler model: keep the file, and a record for every row.
- **Database-level enforcement:** the AI proposed triggers that refuse updates to history, and an exclusion constraint on membership periods. The history guard was adopted once the storage pattern was chosen. The overlap rule stays in application code.
- **Recording previous values:** the AI proposed storing the previous stance and weight on every position change. I asked why, since the previous value is just the earlier entry. We dropped the redundant copy, and the API derives "from → to" instead.
- **History storage pattern:** instead of accepting the AI's first recommendation, I had it write a sourced comparison of six designs, checked against the PostgreSQL and EF Core documentation, then chose with it. When the discussion got abstract, I asked it to draw the entities. The diagram, not the prose, is what made the choice clear.
- **Import classification:** the AI proposed accepting only the two date formats the brief names. I widened it to every unambiguous day-first format. When two conflicts can be pending on one value, I set the rule that a conflict keeps only the incoming value and a link to the revision it was compared against, so resolving one never forces updates to the other. The AI proposed a warning when a date change affects membership; I made it a recalculation on every vote-details decision.
- **Snapshot calculation:** the AI proposed computing the Alignment Index in a single SQL statement, so that about a million country votes would never leave the database. I pushed back: scoring is domain logic, not data. SQL now only streams the facts from one consistent read, and a pure C# function applies the rules, so each rule is unit-tested and readable. The AI's size concern held up poorly: the facts are tens of MB even across all of history.
- **Repository layer:** rather than accept "no repository layer" as stated, I asked for the trade-offs. Three options were laid out side by side. We chose use-case modules for the race-sensitive writes, and plain EF projections for reads.
- **DECISIONS.md:** the AI's first DECISIONS.md was more than three times the two-page limit. I set the outline, moved the reasoning into ADRs, and reframed its plan for four developers. The AI had split future product areas; the plan now describes four people building this system in parallel on a stubbed contract.

### Build

- **Import at scale:** the timed test showed the EF writer missing the 5-minute budget for the historic-sized file (06:50). The AI built a binary COPY writer that met it (03:02). I chose to keep the simpler EF writer and the 200 MB upload limit, and to document the miss and the worker's memory instead. The COPY writer stays in the branch history, and ADR 0005 records the numbers.
- **Race handling:** the AI's first decision code answered any unique violation as "already decided". That included one from a conflict that the membership recalculation opens, which then failed while looking for the winner. It was corrected in the conflicts slice: only the one-decision-per-conflict index now means a lost race.
- **Staying in lane:** the interface slice was meant to change no behaviour, and review caught a new Review link it had added to the import report. It was removed.
- **Pre-submission review:** a review across the merged slices found a swept job's stale handler that could still write, query parameters accepted unchecked, and a year 0000 date that threw instead of being reported invalid. Each was fixed with a test.
- **Azure:** the managed PostgreSQL server could not be reached from the cloud session, so its Entra ID sign-in could not be proven live. Rather than ship a claim it could not check, the database sign-in was removed. The live check covers Blob and Document Intelligence only, and ADR 0009 says so.
- **Unverified results stay marked until they are run:** the real-data test was closed before the revised and historic files had been run, to unblock the next slice. The ticket said which counts were checked by hand and which still needed a run, and the documents carried that caveat until I ran the full real-data suite on the assignment files.
- **This finishing pass:** the new smoke test failed at first because the Imports page has a second file input (for voting sheets). The fix was to target the CSV input, not to loosen the test. The AI also staged the skills trim early, so it landed in the smoke test's commit. I chose to keep that pushed commit, and the trim's ticket records it.
