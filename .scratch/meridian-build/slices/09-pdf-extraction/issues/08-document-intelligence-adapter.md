# 08: Read real sheets with Azure Document Intelligence

**What to build:** The real reader: sends the sheet to Azure Document Intelligence prebuilt-layout and returns cells with text and confidence. Credentials come from configuration, never from the repository.

**Blocked by:** Upload a sheet and watch it extract

**Status:** ready-for-agent

**Requirements:** PDF-02

- [ ] The reader is registered when configured and the app fails with a clear failed-extraction message (not a crash) when it is not
- [ ] Endpoint and key are read from configuration keys that map from DOCINTEL_ENDPOINT and DOCINTEL_KEY in .env; .env.example lists them empty and compose passes them only to the worker
- [ ] The call goes through an instrumented HttpClient inside the job's trace; the log line holds model, latency and outcome, never content
- [ ] Response parsing is tested on a hand-written response fixture
- [ ] A RealData-category smoke test reads a sheet from candidate_data/voting_sheets and skips itself when credentials or files are missing
- [ ] No PDF, key or endpoint is committed
