# Bring in the historic file

Type: task
Status: claimed

## Question

Get `assembly_historic.csv` (~100MB, ~1M rows) into this session's gitignored `data_candidate/` without committing it. Chat upload rejects files this large. Options, cheapest first: upload it zipped or gzipped (CSV usually compresses about 10x); split it into parts the user uploads separately; or put it in the assignment's Azure Blob container and have the agent download it with a SAS URL kept out of git. HITL: the user provides the file; the agent verifies the row count and checksum. Needed by the historic part of [Audit the real data](07-data-audit.md) and by performance work, not by anything earlier.
