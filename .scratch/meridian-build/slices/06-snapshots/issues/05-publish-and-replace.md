# 05: Publish and replace

**What to build:** the admin publishes a calculated snapshot for a reporting quarter they choose (defaulting to the current calendar quarter). Publishing a quarter that already has a publication answers 409 `quarter-published` with the quarter's current publication, and the screen offers "Replace it", which needs a reason and supersedes the current publication. Superseded publications stay visible as "superseded by …, reason …". `/publications` shows each quarter's chain, newest quarter first. Two simultaneous publications for one quarter result in exactly one success and one 409.

**Blocked by:** 03

**Status:** done

- [x] Publishing answers 201 with the publication; a snapshot that is not calculated (job not done) or unknown is refused
- [x] A second first-publication for a quarter answers 409 `quarter-published` with the current publication; replacing anything but the quarter's current publication answers 409 too
- [x] A replacement without a reason answers 400 `validation` on `reason`; with one, it supersedes and both stay in the chain
- [x] The race test runs 20 rounds of two simultaneous publications: exactly one 201 and one 409 `quarter-published` each round
- [x] `GET /api/publications` returns each quarter's chain with the current publication and superseded-by links; analysts can read it, only the admin can publish
- [x] The snapshot page has Publish (disabled with "Admin only" for analysts) with the 409 card and "Replace it"; `/publications` shows the chains
