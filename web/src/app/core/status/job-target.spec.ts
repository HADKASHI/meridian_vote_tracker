import { JobDto } from '../../api/model';
import { jobTarget } from './job-target';

const job = (fields: Partial<JobDto>) => fields as JobDto;

describe('jobTarget', () => {
  it('points an import job at its import', () => {
    expect(jobTarget(job({ kind: 'import', importId: 4 }))).toEqual({ label: 'Import #4', link: ['/imports', 4] });
  });

  it('points a snapshot job at its snapshot', () => {
    expect(jobTarget(job({ kind: 'snapshot', snapshotId: 7 }))).toEqual({ label: 'Snapshot #7', link: ['/snapshots', 7] });
  });

  it('points a PDF extraction job at its voting sheet, not at a snapshot', () => {
    expect(jobTarget(job({ kind: 'pdf_extraction', extractionId: 9 }))).toEqual({
      label: 'Voting sheet #9',
      link: ['/review/extractions', 9],
    });
  });
});
