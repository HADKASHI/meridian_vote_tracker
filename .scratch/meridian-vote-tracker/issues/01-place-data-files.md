# Place the data files locally

Type: task
Status: open

## Question

Get the `data_candidate/` files (member_states.csv, assembly_export_2024_q1.csv, assembly_export_2024_q1_rev2.csv, assembly_historic.csv, voting_sheets/) into a gitignored `data_candidate/` folder at the repo root without them ever entering git history. HITL: the user uploads them, the agent adds the .gitignore entry, copies them in, and checks `git status` shows nothing to commit. Record file sizes and row counts for later tickets.
