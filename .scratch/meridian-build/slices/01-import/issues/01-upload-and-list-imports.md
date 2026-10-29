# 01: Upload and list imports

**What to build:** an analyst picks a CSV on `/imports` and uploads it. `POST /api/imports` streams the file to Blob (`imports/{importId}/{filename}`) while hashing it, then inserts the import (file name, SHA-256, size, uploader, upload time) and its queued job in one transaction, and answers 202 with the import id and job id. A file over `Imports:MaxUploadBytes` is refused with 413, and a request with no file with a validation problem. `GET /api/imports` lists imports newest first with uploader, time, job state and outcome counts; `GET /api/imports/{importId}` returns the import with its job (queued with place in queue, running with progress, done or failed), 404 when unknown. The `/imports` page lists imports and has the upload button, which navigates to `/imports/:importId`; the report page shows the file's evidence and polls the job every second while it is queued or running.

**Blocked by:** None (can start immediately)

**Status:** done

Requirements: ING-01, ING-02, ING-15 (stories 9, 10, 11, 12, 18, 49)

- [x] Uploading a file returns 202 with ids; the blob holds the exact bytes and the import's SHA-256 matches them
- [x] The import and its job are visible through `GET /api/imports/{importId}` and in `GET /api/imports`, with the job queued and its place in queue
- [x] A missing file is a validation problem; an unknown import is 404
- [x] The job stores the upload request's trace parent
- [x] The Angular imports list uploads a file and opens its report, which polls while the job is active and stops once it is done or failed (Vitest)
