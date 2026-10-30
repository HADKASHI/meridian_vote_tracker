# 07: Guided live check of the Azure services

**What to build:** a guided script the operator runs on their own machine with their own `.env` that proves Blob upload and download, the database on its real version, a Document Intelligence call on a real sheet and Application Insights, and prints what passed and what did not. No secret is stored, logged or sent anywhere else, and no real sheet is committed. Running it closes the fixture-only limit in ADR 0008.

**Blocked by:** 05

**Status:** ready-for-agent

- [ ] The script checks Blob (upload then download compares the bytes), the database (connects with TLS and prints its real version), Document Intelligence (one call on a sheet path the operator gives) and Application Insights (sends and says what to look for)
- [ ] It reads the operator's `.env`, prints a pass or fail line per check with the reason for a failure, and exits non-zero when any fails
- [ ] No secret or sheet content is printed, written to a file or sent to any other service; the sheet is read from where the operator points and never copied into the repository
- [ ] It runs without Azure in this environment up to a clear "not configured" result per check, and the part that can be tested without credentials is tested
- [ ] `docs/azure.md` explains how to run it and what each result means
- [ ] No migration is added
