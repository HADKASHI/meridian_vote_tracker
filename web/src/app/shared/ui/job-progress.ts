import { JobDto, JobState } from '../../api/model';

/** The words a job's progress is shown in, everywhere. */
export function jobProgress(job: JobDto): string {
  switch (job.state) {
    case JobState.queued:
      return job.placeInQueue === 1 ? 'Next in queue' : `${(job.placeInQueue ?? 1) - 1} ahead in queue`;
    case JobState.running:
      return job.votesTotal
        ? `${job.votesDone.toLocaleString('en')} of ${job.votesTotal.toLocaleString('en')} votes`
        : `${job.rowsRead.toLocaleString('en')} rows read`;
    case JobState.done:
      return job.votesTotal ? `${job.votesTotal.toLocaleString('en')} votes` : 'Done';
    case JobState.failed:
      return job.votesTotal
        ? `Failed after ${job.votesDone.toLocaleString('en')} of ${job.votesTotal.toLocaleString('en')} votes: ${job.lastError ?? ''}`
        : `Failed: ${job.lastError ?? ''}`;
  }
}
