import { ExtractionDto, ExtractionSummaryDto, JobDto, PageDtoOfExtractionSummaryDto, SheetRow } from '../../api/model';

// Response bodies for the extractions specs.

export function job(overrides: Partial<JobDto> = {}): JobDto {
  return {
    id: 5, kind: 'pdf_extraction', state: 'done', importId: null, snapshotId: null, extractionId: 3, placeInQueue: null, rowsRead: 0,
    votesDone: 0, votesTotal: null, attempts: 1, lastError: null, traceId: null, startedBy: 'dana', createdAt: '2024-04-02T09:00:00Z',
    startedAt: '2024-04-02T09:00:01Z', finishedAt: '2024-04-02T09:00:05Z',
    ...overrides,
  };
}

export function summary(overrides: Partial<ExtractionSummaryDto> = {}): ExtractionSummaryDto {
  return {
    id: 3, fileName: 'MA_2011_042.pdf', uploadedBy: 'dana', uploadedAt: '2024-04-02T09:00:00Z', sizeBytes: 1000, status: 'needs_review',
    voteId: 'MA/2011/042', job: job(), blockingIssues: 0, warnings: 0,
    ...overrides,
  };
}

export function page(items: ExtractionSummaryDto[]): PageDtoOfExtractionSummaryDto {
  return { items, total: items.length, page: 1, pageSize: 200 };
}

export function row(position: number, code: string, overrides: Partial<SheetRow> = {}): SheetRow {
  return { code, name: `State ${code}`, choice: 'y', ambiguous: false, confidence: 0.96, page: 1, position, ...overrides };
}

/** A sheet waiting for review whose counts match the totals it prints, unless the test says otherwise. */
export function extraction(overrides: Partial<ExtractionDto> = {}): ExtractionDto {
  return {
    id: 3, fileName: 'MA_2011_042.pdf', sha256: 'ab', sizeBytes: 1000, uploadedBy: 'dana', uploadedAt: '2024-04-02T09:00:00Z',
    status: 'needs_review', job: job(), error: null, voteId: 'MA/2011/042',
    header: { voteId: 'MA/2011/042', session: '66', date: '4 October 2011', title: 'Protection of Cultural Heritage' },
    rows: [row(1, 'ARG'), row(2, 'BRA', { choice: 'n' })],
    counted: { yes: 1, no: 1, abstentions: 0, notVoting: 0 }, printed: { yes: 1, no: 1, abstentions: 0, notVoting: 0 },
    issues: [], canApprove: true, edits: [], decision: null, importId: null,
    ...overrides,
  };
}

/** The same sheet with one row whose two marks the reader could not settle, so its counts miss the printed totals. */
export function blocked(): ExtractionDto {
  return extraction({
    rows: [row(1, 'ARG', { choice: null, ambiguous: true, confidence: 0.6 }), row(2, 'BRA', { choice: 'n' })],
    counted: { yes: 0, no: 1, abstentions: 0, notVoting: 0 },
    issues: [
      { severity: 'blocking', kind: 'ambiguous_mark', message: 'ARG has more than one mark.', code: 'ARG' },
      { severity: 'blocking', kind: 'totals_mismatch', message: 'Counted Yes 0, No 1 but the sheet prints Yes 1, No 1.' },
      { severity: 'warning', kind: 'vote_id_mismatch', message: 'The vote number on the sheet does not match the file name.' },
    ],
    canApprove: false,
  });
}
