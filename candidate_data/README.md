# Assignment data

Put the assignment's files here. Everything in this folder except this README is ignored by git and kept out of every Docker build, so client data never leaves your machine.

- `member_states.csv`: required. `docker compose up` seeds member states from it and stops with a clear message if it is missing.
- `assembly_export_2024_q1.csv`, `assembly_export_2024_q1_rev2.csv`, `assembly_historic.csv`: upload these through the Imports page.
- `voting_sheets/`: the scanned PDFs, for the PDF extraction bonus.

The real-data tests read the exports from here and skip themselves when the files are missing.
