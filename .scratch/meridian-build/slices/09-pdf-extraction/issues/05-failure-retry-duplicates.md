# 05: Show failures and let the analyst retry

**What to build:** When extraction fails the analyst sees what happened, nothing crashes, and they can try again. Uploading the same file twice does not create a second extraction.

**Blocked by:** Upload a sheet and watch it extract

**Status:** ready-for-agent

**Requirements:** PDF-06

- [ ] A transient reader error uses the job's retries, then fails the extraction
- [ ] A permanent failure (timeout, not a PDF, empty or protected file, a 4xx from the service) sets status failed with a message-only error
- [ ] POST retry on a failed extraction queues a fresh job and returns it to extracting; retry on any other status is refused
- [ ] Uploading the same bytes returns the existing extraction
- [ ] Tests cover each failure kind through the fake reader
