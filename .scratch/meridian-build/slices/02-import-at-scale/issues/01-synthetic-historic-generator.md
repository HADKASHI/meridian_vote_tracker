# 01: Synthetic historic file generator

**What to build:** A deterministic generator that produces, from a fixed seed, a file shaped like the historic audit (map ticket 17): 6,480 votes of 147–151 rows each, fully interleaved by vote, 150 member codes, with a scattering of each defect kind (exact duplicates, truncated rows, rows shifted by an unquoted comma, placeholder codes, bad ballots, a vote cast before the state joined). It also produces the matching 150-state member-states file and the exact outcome counts an import of the file must give. The file is never committed.

**Blocked by:** None (can start immediately).

**Status:** done

- [x] The same seed gives byte-identical output; a different seed gives different output
- [x] The file has 6,480 votes, 147–151 rows per vote and 150 member codes, and is interleaved so no vote's rows are contiguous
- [x] Every defect kind appears, and the expected counts (new, duplicate, invalid, contradiction 0) are returned alongside the file
- [x] Classifying the generated rows with the real parser and classifier gives exactly the expected counts
- [x] The member-states file is generated too, and nothing generated is committed
