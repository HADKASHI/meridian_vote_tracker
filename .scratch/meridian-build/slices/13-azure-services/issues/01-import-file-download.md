# 01: Download an import's original file

**What to build:** an analyst or the admin downloads the original file of any import (a CSV or an approved voting sheet PDF) through the API. One shared download helper beside the upload helper in `Meridian.Platform` streams the stored blob as an attachment with the import's original file name and a content type derived from its extension (the import stores the name, not the type; no migration). There is no public or SAS link, so access stays behind the role checks and each download is logged with ids and size, never content (ADR 0009).

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] `GET /api/imports/{importId}/file` streams the stored original from Blob with its original name and content type (`text/csv` for a CSV, `application/pdf` for a sheet) as an attachment
- [ ] An unknown import answers 404 with the usual problem body; a missing blob is a clear error, not a crash
- [ ] Analysts and the admin may download; nobody (401) and the wrong role (403) are proven by the generated role matrix
- [ ] The download is logged with the import id and size and no file content or file name
- [ ] The helper is shared, lives beside the upload helper and takes what ticket 02 needs (blob name, file name), so no second copy is written there
- [ ] Integration tests through the HTTP API on Azurite download a fixture CSV import and an approved voting sheet import and get the same bytes, name and content type back
- [ ] The OpenAPI file is regenerated for this endpoint only; no migration is added
