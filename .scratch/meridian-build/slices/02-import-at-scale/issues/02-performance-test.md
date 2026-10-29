# 02: Performance test

**What to build:** A `Performance` test that imports the generated file through the real API and worker (production job timeout), fails when the import takes longer than 5 minutes, checks the outcome counts, and reports elapsed time and peak memory. It runs on demand and never in CI.

**Blocked by:** 01 Synthetic historic file generator.

**Status:** ready-for-agent

- [ ] The test runs on demand under the `Performance` category and CI does not run it
- [ ] It fails when the import takes longer than 5 minutes
- [ ] The outcome counts equal the generator's expected counts and the job is done
- [ ] Elapsed time and peak memory are reported in the test output
