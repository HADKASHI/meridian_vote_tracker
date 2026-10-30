# 03: Download buttons in the app

**What to build:** the Imports list and the import page offer Download for CSV and PDF imports, and the Extractions tab in Review offers it for a sheet waiting in review, each using the endpoints from tickets 01 and 02 and the regenerated client.

**Blocked by:** 01, 02

**Status:** ready-for-agent

- [ ] Each row of the Imports list and the import page show a Download action that fetches `/api/imports/{id}/file`
- [ ] The Extractions tab and the extraction page show a Download action for a sheet waiting in review
- [ ] A failed download (404, 403) is shown with the shared error notice, with its trace id
- [ ] Vitest tests with `HttpTestingController` cover the three places and the error notice
- [ ] The generated client is regenerated from the OpenAPI file, not edited; lint, test and build pass
