# 04: Invalid rows and normalisations

**What to build:** map ticket 08 rule 2 and the invalid checks of rules 3 and 12, as pure domain functions with one named test per rule. Stored values are trimmed, Unicode NFC, control and invisible characters removed (zero-width, NBSP to space, stray `\r`); country codes and single-letter ballots upper-cased; dates converted to ISO from `yyyy-mm-dd`, `yyyy/mm/dd`, `dd-mm-yyyy`, `dd/mm/yyyy`, `dd.mm.yyyy` (leading zeros optional) and the long form `14 March 2007`, numeric dates always day-first. The comparison key is the stored value case-folded, whitespace runs collapsed, typographic quotes and dashes unified, accents kept. A row is invalid, with a specific reason code and message, for: wrong field count, a missing required field, an unknown country code (never guessed), a ballot that isn't Y/N/A/X (ballot words like `Yes` included), an empty ballot, an ambiguous or impossible date (month-first, 2-digit year, time part, impossible day), a session that isn't a positive integer, or a state that was not a member on the vote date (inclusive at both ends, empty end means still a member). Each normalisation applied is recorded on the row record and shown in the report.

**Blocked by:** 03

**Status:** done

Requirements: ING-04, ING-06, ING-07, ING-13 (stories 25, 26, 27, 28, 29)

- [x] A named domain test for each normalisation and each invalid reason above
- [x] Membership boundaries: the first and last day are members, the day after is not, an open period is
- [x] Through the API, an invalid-rows fixture gives one invalid row per reason with its code, and nothing approved for them
- [x] A normalisations fixture stores the normalised values and lists each normalisation on its row
