# 05: Show failures and let the analyst retry

**What to build:** When extraction fails the analyst sees what happened, nothing crashes, and they can try again. Uploading the same file twice does not create a second extraction.

**Blocked by:** Upload a sheet and watch it extract

**Status:** done

**Requirements:** PDF-06

- [x] A transient reader error uses the job's retries, then fails the extraction
- [x] A permanent failure (not a PDF, empty file, a refusal the reader marks permanent such as a protected file or a 4xx) sets status failed with a message-only error and ends the job without retries; a job that fails (timeout, retries used up, a stopped worker) shows its extraction as failed with the job's message
- [x] POST retry on a failed extraction requeues its job (the one job row of the extraction, reset: the schema keeps one job per extraction) and returns it to extracting; retry on any other status is refused with 409 not-retryable
- [x] Uploading the same bytes returns the existing extraction
- [x] Tests cover each failure kind through the fake reader
