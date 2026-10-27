# Choose how data is read and written

Type: grilling
Status: claimed
Blocked by: 02

## Question

Which data-access approach, and why (section 9 asks us to explain it)? Likely EF Core with migrations for the model and constraints, plus raw SQL or PostgreSQL COPY for the bulk paths (a ~1M-row historic import, snapshot computation over all history). Decide where each is used and how migrations run under docker compose.
