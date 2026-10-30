# 02: Download a sheet waiting in Review

**What to build:** an analyst or the admin downloads the original PDF of a voting sheet that is still waiting in Review (or in any other extraction state), through the same helper as ticket 01. The extraction stores its file name, so the name is the one uploaded and the vote number is not needed to derive one.

**Blocked by:** 01

**Status:** done

- [x] `GET /api/extractions/{extractionId}/file` streams the stored PDF as an attachment with its original name and `application/pdf`, for an extraction in any status
- [x] An unknown extraction answers 404 with the usual problem body
- [x] Nobody (401) and the wrong role (403) are proven by the generated role matrix
- [x] The download is logged with the extraction id and size and no content
- [x] It uses the shared helper from ticket 01
- [x] An integration test through the HTTP API uploads a sheet with the fake reader, leaves it in review and downloads identical bytes
- [x] The OpenAPI file is regenerated for this endpoint only; no migration is added
