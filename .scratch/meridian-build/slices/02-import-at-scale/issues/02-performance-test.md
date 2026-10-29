# 02: Performance test

**What to build:** A `Performance` test that imports the generated file through the real API and worker (production job timeout), fails when the import takes longer than 5 minutes, checks the outcome counts, and reports elapsed time and peak memory. It runs on demand and never in CI.

**Blocked by:** 01 Synthetic historic file generator.

**Status:** done

- [x] The test runs on demand under the `Performance` category and CI does not run it
- [x] It fails when the import takes longer than 5 minutes
- [x] The outcome counts equal the generator's expected counts and the job is done
- [x] Elapsed time and peak memory are reported in the test output

## Comments

- First run in the build container (Postgres in Docker on the same machine): 971,327 rows in 07:07.6, peak working set 2,151 MiB (1,420 MiB above the 731 MiB before the import). The counts match the generator's; only the time budget fails, so ticket 04 needs a fix ticket.
