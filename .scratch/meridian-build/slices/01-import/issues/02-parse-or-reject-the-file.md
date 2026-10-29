# 02: Parse the file, or reject it

**What to build:** the import job handler reads the uploaded file from Blob and parses it (map ticket 08 rule 1, with `resolution_title` as the title column per map ticket 17). Columns are matched by header name, trimmed and case-insensitive, in any order; required columns are `assembly_vote_id`, `session`, `vote_date`, `resolution_title`, `country_code`, `ballot`, optional `country_name` and `published_at`; any other column is ignored and noted as an `extra_columns` warning. A BOM is stripped and recorded; line endings are recorded as `lf`, `crlf` or `mixed`. A file with a missing required column, invalid UTF-8, or no data rows is **rejected**: its parse record carries the reason, it has no row records, nothing is applied, and the job ends `done`. Every other file gets a parse record (header as found, BOM, line endings, data rows, newest `published_at`). The parse record is written once (`ON CONFLICT DO NOTHING` on a retry). The report shows the parse facts, the rejection reason, and the warnings.

**Blocked by:** 01

**Status:** done

Requirements: ING-03, ING-04 (stories 19, 20, 21, 22, 23)

- [x] Domain tests for rule 1: header order and case, `resolution_title`, BOM and no BOM, LF, CRLF and mixed, extra columns, missing required column, invalid UTF-8, header only
- [x] Through the API, each rejected-file fixture ends with its reason, no row records and nothing approved
- [x] An extra column gives an `extra_columns` warning naming it
- [x] The report page shows the rejection reason or the parse facts and warnings
