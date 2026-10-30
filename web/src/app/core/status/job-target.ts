import { JobDto } from '../../api/model';

/** What a job works on, as words and as the route that shows it: the one place that knows each job kind's subject. */
export function jobTarget(job: JobDto): { label: string; link: (string | number)[] } {
  switch (job.kind) {
    case 'import':
      return { label: `Import #${job.importId}`, link: ['/imports', job.importId!] };
    case 'pdf_extraction':
      return { label: `Voting sheet #${job.extractionId}`, link: ['/review/extractions', job.extractionId!] };
    default:
      return { label: `Snapshot #${job.snapshotId}`, link: ['/snapshots', job.snapshotId!] };
  }
}
