# Decide concurrency and roles

Type: grilling
Status: claimed

## Question

How do we guarantee that only one of two simultaneous conflict decisions takes effect, with a clear message and the current state shown to the loser (CON-05)? How do users identify themselves (pre-seeded Dana, Omar, Lena with a user switcher) and how are roles enforced in the API, not only in the UI (section 3, POS-01, SNP-05)? Cover optimistic concurrency vs conditional updates, the error contract for a lost race (409 shape), and admin-only actions.
