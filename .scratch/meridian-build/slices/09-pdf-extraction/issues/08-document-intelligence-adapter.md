# 08: Read real sheets with Azure Document Intelligence

**What to build:** The real reader: sends the sheet to Azure Document Intelligence prebuilt-layout and returns cells with text and confidence. Credentials come from configuration, never from the repository.

**Blocked by:** Upload a sheet and watch it extract

**Status:** done

**Requirements:** PDF-02

- [x] The reader is registered when configured and a sheet fails once, with a clear failed-extraction message (not a crash), when it is not; Retry works after configuring
- [x] Endpoint and key are read from configuration keys that map from DOCINTEL_ENDPOINT and DOCINTEL_KEY in .env; .env.example lists them empty and compose passes them only to the worker
- [x] The call goes through an instrumented HttpClient inside the job's trace; the log line holds model, latency and outcome, never content
- [x] Response parsing is tested on a hand-written response fixture
- [x] A RealData-category smoke test reads a sheet from candidate_data/voting_sheets and skips itself when credentials or files are missing (not run here: no Azure credentials in the build session, and the Document Intelligence docs were not reachable, so the request and response shapes are the documented v4 ones from memory and are proven only against the hand-written fixture until someone runs the smoke test)
- [x] No PDF, key or endpoint is committed
