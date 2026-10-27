# AI usage

## Tools

Claude Code, driven by a small set of skills in `.claude/skills/`:
- **wayfinder** keeps a decision map of tickets in `.scratch/meridian-vote-tracker/`.
- **grilling** and **domain-modeling** run design interviews and maintain the glossary.
- **research** runs background investigations against primary sources.

The AI proposes options with a recommendation; I decide.

## Where AI output was checked, rejected or corrected

- **Data audit:** the AI catalogued 25 defect classes in the candidate files. Its headline counts (vote 040 missing from rev2, vote 031 truncated, the slash dates, the 5-field row) were re-counted independently before being relied on. It quoted data rows in its report; these were removed, so the client's data stays out of git.
- **Keeping uploaded files:** the AI first proposed copying a row's raw text only for rejected or flagged rows. Questioning what the system must answer at any moment led to a simpler model: keep the file, and a record for every row.
- **Database-level enforcement:** the AI proposed triggers that refuse updates to history, and an exclusion constraint on membership periods. Both are deferred. The history guard is to be revisited once the storage pattern is chosen; the overlap rule is enforced in application code.
- **History storage pattern:** instead of accepting the AI's first recommendation, I had it write a sourced comparison of six designs, and the choice is made separately.
