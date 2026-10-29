# 07: File warnings

**What to build:** map ticket 08 rules 9 to 11 as import warnings, shown on the report. Uploading a file with the same SHA-256 as an earlier import imports it normally with an `identical_file` warning naming that import, and the report shows nothing changed. A file whose newest `published_at` is older than the newest already imported gets an `older_published_at` warning; `published_at` is never used for ordering. After the import, each vote it touched that has members on the vote date with no approved country vote gets an `incomplete_vote` warning listing them. Absence never changes approved data and gets no outcome.

**Blocked by:** 06

**Status:** done

Requirements: ING-09, ING-12 (stories 43, 44, 45, 46)

- [x] Re-uploading the same file is warned as identical to the first import and every row is unchanged (or links to its existing conflict); approved data and conflicts are as before
- [x] An older `published_at` is warned with the newest one already imported
- [x] An incomplete vote is warned with its missing states; a complete one is not
