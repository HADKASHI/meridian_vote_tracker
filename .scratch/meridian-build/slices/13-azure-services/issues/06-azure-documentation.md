# 06: Document running on Azure

**What to build:** `docs/azure.md` tells an operator how to run the app on Azure Blob Storage and Azure Database for PostgreSQL: the override file, the `.env` variables, TLS, the empty or already migrated dedicated database, the supported PostgreSQL versions, and Application Insights as configuration only (setting its connection string switches it on). The README is not edited; the finishing pass (slice 8) links this file.

**Blocked by:** 05

**Status:** done

- [x] `docs/azure.md` covers the commands, every variable and the startup message for a failing connection
- [x] It records PostgreSQL 17 and 18 as supported and states that a new database must be dedicated to this app
- [x] It says Application Insights is configuration only and which setting turns it on
- [x] It points to the guided check (ticket 07) and says real sheets and credentials never leave the operator's machine
- [x] The README is untouched
