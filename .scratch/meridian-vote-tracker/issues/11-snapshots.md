# Design snapshots

Type: grilling
Status: open
Blocked by: 04, 05, 15

## Question

How is a snapshot stored so it is immutable and reproduces exactly in a year (SNP-04) and answers "which data was the published index based on": frozen per-vote inputs (values, positions, weights) vs references to revision ids? How is it computed over ~1M country votes without blocking (SNP-02), selected by date range, session or list (SNP-01), and blocked or warned when a selected vote has no position (POS-04)? What does "one published snapshot per quarter" mean exactly: which quarter, and can a published one be replaced (SNP-05)?
