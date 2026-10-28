# 08: Crash resume

**What to build:** a retried import resumes from the votes it already committed: it skips votes that already have row records for this import, writes the rest, and its warnings and report end the same as an uninterrupted run. Progress counts the skipped votes as done. A failure is recorded with how far it got ("failed after 812 of 1,204 votes").

**Blocked by:** 06

**Status:** done

Requirements: ING-02 (stories 14, 15, 16)

- [x] An import interrupted after some votes, then retried, gives the same row records, approved values, conflicts and warnings as an uninterrupted import of the same file, with no row written twice
- [x] Proven through the real `BackgroundService`: a worker stopped mid-import and a new one started finish the import
- [x] A failed import's error says how many votes it committed
