# Bring in the historic file

Type: task
Status: resolved

## Question

Get `assembly_historic.csv` (~100MB, ~1M rows) into this session's gitignored `candidate_data/` without committing it. Chat upload rejects files this large. Options, cheapest first: upload it zipped or gzipped (CSV usually compresses about 10x); split it into parts the user uploads separately; or put it in the assignment's Azure Blob container and have the agent download it with a SAS URL kept out of git. HITL: the user provides the file; the agent verifies the row count and checksum. Needed by the historic part of [Audit the real data](07-data-audit.md) and by performance work, not by anything earlier.

## Answer

The user uploaded `assembly_historic.zip` (16.2 MB, about 6.4× compression) through chat. It was extracted into the gitignored `candidate_data/` (`git check-ignore` confirms the `/candidate_data/` rule covers it). Nothing was committed.

- **File**: `candidate_data/assembly_historic.csv`, 103,625,965 bytes
- **SHA-256**: `08bbb0201824c95136809dba9c53cea94ad38a48e64c41895c0d011e20dec782`
- **Rows**: 962,171 data rows plus a header (962,172 lines). The brief's "~1M" is about right.
- **Header**: same 8 columns as the Q1 exports: `assembly_vote_id,session,vote_date,resolution_title,country_code,country_name,ballot,published_at`
- **First look (not an audit)**: 6,480 distinct vote ids, 153 distinct country codes, `vote_date` values running roughly 2015–2023 (raw string min/max, before any normalisation)

Fact for later sessions: `candidate_data/` is lost whenever the container is reclaimed. This container has **only** the historic file. member_states, both Q1 exports and the voting sheets from [Place the data files locally](01-place-data-files.md) must be uploaded again before any session that needs them. Check the checksum above to confirm a re-upload is the same file.

The historic audit is now its own ticket: [Audit the historic file](17-historic-audit.md).
