import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { ImportDto, JobDto, PageDtoOfImportSummaryDto } from '../../api/model';
import routes from './imports.routes';

const job = (state: JobDto['state'], extra: Partial<JobDto> = {}): JobDto => ({
  id: 3, kind: 'import', state, importId: 7, snapshotId: null, placeInQueue: state === 'queued' ? 1 : null,
  rowsRead: 0, votesDone: 0, votesTotal: null, attempts: state === 'queued' ? 0 : 1, lastError: null,
  traceId: null, startedBy: 'dana', createdAt: '2024-01-01T00:00:00Z', startedAt: null, finishedAt: null, ...extra,
});
const counts = { new: 0, unchanged: 0, duplicate: 0, invalid: 0, contradiction: 0, conflict: 0 };
const importDto = (state: JobDto['state'], extra: Partial<JobDto> = {}): ImportDto => ({
  id: 7, fileName: 'q1.csv', sha256: 'ab'.repeat(32), sizeBytes: 120, uploadedBy: 'dana',
  uploadedAt: '2024-01-01T00:00:00Z', job: job(state, extra), parse: null, counts, warnings: [], conflictsOpened: [],
});

/** Renders after a response without waiting for requests still pending (whenStable would). */
async function settle(): Promise<void> {
  TestBed.tick();
  await new Promise((resolve) => setTimeout(resolve));
  TestBed.tick();
}

describe('imports feature', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: 'imports', children: routes }], withComponentInputBinding()),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => vi.useRealTimers());

  it('lists imports with uploader, job state and counts', async () => {
    const harness = await RouterTestingHarness.create('/imports');
    const page: PageDtoOfImportSummaryDto = {
      items: [{ id: 7, fileName: 'q1.csv', uploadedBy: 'dana', uploadedAt: '2024-01-01T00:00:00Z', sizeBytes: 120,
        job: job('done'), rejectedReason: null, counts: { ...counts, new: 5, invalid: 2 } }],
      total: 1, page: 1, pageSize: 50,
    };
    http.expectOne((r) => r.url === '/api/imports').flush(page);
    await harness.fixture.whenStable();

    const text = harness.routeNativeElement!.textContent!;
    expect(text).toContain('q1.csv');
    expect(text).toContain('dana');
    expect(text).toContain('done');
    const chips = [...harness.routeNativeElement!.querySelectorAll('[data-testid=imports-table] app-outcome-chip')];
    expect(chips.map((c) => c.textContent!.replace(/\s+/g, ' ').trim())).toEqual(['new 5', 'invalid 2']);
  });

  it('uploads the chosen file and opens its report', async () => {
    const harness = await RouterTestingHarness.create('/imports');
    http.expectOne((r) => r.url === '/api/imports' && r.method === 'GET')
      .flush({ items: [], total: 0, page: 1, pageSize: 50 });
    await harness.fixture.whenStable();

    const input = harness.routeNativeElement!.querySelector<HTMLInputElement>('input[type=file]')!;
    const file = new File(['a,b\n'], 'q1.csv', { type: 'text/csv' });
    Object.defineProperty(input, 'files', { value: [file] });
    input.dispatchEvent(new Event('change'));

    const upload = http.expectOne((r) => r.url === '/api/imports' && r.method === 'POST');
    expect((upload.request.body as FormData).get('file')).toBe(file);
    upload.flush({ importId: 7, jobId: 3 });

    await vi.waitFor(() => expect(TestBed.inject(Router).url).toBe('/imports/7'));
  });

  it('shows an upload refused by the API on the page', async () => {
    const harness = await RouterTestingHarness.create('/imports');
    http.expectOne((r) => r.method === 'GET').flush({ items: [], total: 0, page: 1, pageSize: 50 });
    await harness.fixture.whenStable();

    const input = harness.routeNativeElement!.querySelector<HTMLInputElement>('input[type=file]')!;
    Object.defineProperty(input, 'files', { value: [new File(['x'], 'big.csv')] });
    input.dispatchEvent(new Event('change'));
    http.expectOne((r) => r.method === 'POST').flush(
      { type: 'payload-too-large', title: 'The file is too large', status: 413, detail: 'Files up to 200 MB can be imported.', traceId: 't1' },
      { status: 413, statusText: 'Payload Too Large' },
    );
    await harness.fixture.whenStable();

    expect(harness.routeNativeElement!.textContent).toContain('Files up to 200 MB can be imported.');
  });

  it('polls the report every second while its job is active, then stops', async () => {
    const harness = await RouterTestingHarness.create('/imports/7');
    vi.useFakeTimers();
    http.expectOne('/api/imports/7').flush(importDto('queued'));
    TestBed.tick();
    await vi.advanceTimersByTimeAsync(0);
    expect(harness.routeNativeElement!.textContent).toContain('Next in queue');
    expect(harness.routeNativeElement!.querySelector('h1')!.textContent).toBe('Import #7 · q1.csv');

    await vi.advanceTimersByTimeAsync(1000);
    TestBed.tick();
    http.expectOne('/api/imports/7').flush(importDto('running', { votesDone: 3, votesTotal: 40 }));
    TestBed.tick();
    await vi.advanceTimersByTimeAsync(0);
    expect(harness.routeNativeElement!.textContent).toContain('3 of 40 votes');

    await vi.advanceTimersByTimeAsync(1000);
    TestBed.tick();
    http.expectOne('/api/imports/7').flush(importDto('done', { votesDone: 40, votesTotal: 40 }));
    TestBed.tick();
    await vi.advanceTimersByTimeAsync(5000);
    TestBed.tick();
    http.expectNone('/api/imports/7');
  });

  it('shows why a file was rejected', async () => {
    const harness = await RouterTestingHarness.create('/imports/7');
    http.expectOne('/api/imports/7').flush({
      ...importDto('done'),
      parse: { header: ['a'], hasBom: false, lineEndings: 'lf', dataRows: 0, maxPublishedAt: null,
        rejectedReason: 'Missing required column: ballot.' },
    });
    await harness.fixture.whenStable();

    expect(harness.routeNativeElement!.textContent).toContain('Rejected: Missing required column: ballot.');
  });

  it('shows the parse facts and the warnings', async () => {
    const harness = await RouterTestingHarness.create('/imports/7');
    http.expectOne('/api/imports/7').flush({
      ...importDto('done'),
      parse: { header: ['a'], hasBom: true, lineEndings: 'crlf', dataRows: 1204, maxPublishedAt: null, rejectedReason: null },
      warnings: [
        { id: 1, kind: 'extra_columns', message: 'Ignored column not in the export format: region.', vote: null,
          identicalToImportId: null, columns: ['region'], missingStateCodes: null, newestPublishedAt: null },
        { id: 2, kind: 'incomplete_vote', message: 'Incomplete vote: 2 members on its date with no approved ballot.',
          vote: { voteId: 41, assemblyVoteId: 'MA/2024/001' }, identicalToImportId: null, columns: null,
          missingStateCodes: ['FRA', 'JPN'], newestPublishedAt: null },
      ],
    });
    await settle();

    const text = harness.routeNativeElement!.textContent!;
    expect(text).toContain('1,204 data rows');
    expect(text).toContain('CRLF');
    expect(text).toContain('with BOM');
    expect(text).toContain('Ignored column not in the export format: region.');
    expect(text).toContain('Missing: FRA, JPN');
  });

  it('collapses a warning naming many missing states to their count, which expands', async () => {
    const missing = Array.from({ length: 69 }, (_, i) => `S${String(i).padStart(2, '0')}`);
    const harness = await RouterTestingHarness.create('/imports/7');
    http.expectOne('/api/imports/7').flush({
      ...importDto('done'),
      warnings: [
        { id: 2, kind: 'incomplete_vote', message: 'Incomplete vote: 69 members on its date with no approved ballot.',
          vote: { voteId: 41, assemblyVoteId: 'MA/2024/001' }, identicalToImportId: null, columns: null,
          missingStateCodes: missing, newestPublishedAt: null },
      ],
    });
    await settle();

    const warnings = harness.routeNativeElement!.querySelector('[data-testid=warnings]')!;
    const toggle = warnings.querySelector('button')!;
    expect(toggle.textContent!.trim()).toBe('69 missing states');
    expect(warnings.textContent).not.toContain('S68');
    toggle.click();
    await settle();
    expect(warnings.textContent).toContain('S68');
  });

  it('shows counts as filters, the counts per vote and the rows of the chosen outcome', async () => {
    const harness = await RouterTestingHarness.create('/imports/7?outcome=invalid');
    http.expectOne('/api/imports/7').flush({ ...importDto('done'), counts: { ...counts, new: 3, invalid: 1 } });
    await settle();
    http.expectOne('/api/imports/7/votes').flush([
      { vote: { voteId: 41, assemblyVoteId: 'MA/2024/001' }, counts: { ...counts, new: 3, invalid: 1 } },
    ]);
    const rows = http.expectOne((r) => r.url === '/api/imports/7/rows');
    expect(rows.request.params.get('outcome')).toBe('invalid');
    expect(rows.request.params.get('page')).toBe('1');
    rows.flush({
      items: [{ id: 90, lineNo: 4, rawText: 'MA/2024/001,79,2024-01-10,A,FRN,X', outcome: 'invalid',
        reasonCode: 'unknown_country_code', reasonMessage: 'Unknown country code FRN.', normalisations: [],
        vote: { voteId: 41, assemblyVoteId: 'MA/2024/001' }, stateCode: 'FRN', countryVoteRevisionId: null,
        detailsRevisionId: null, conflictIds: [] }],
      total: 1, page: 1, pageSize: 100,
    });
    await settle();

    const page = harness.routeNativeElement!;
    const filters = [...page.querySelectorAll<HTMLAnchorElement>('[data-testid=outcome-filters] a')];
    expect(filters.map((a) => a.textContent!.trim())).toEqual(['All 4', 'new 3', 'unchanged 0', 'duplicate 0', 'invalid 1', 'contradiction 0', 'conflict 0']);
    expect(filters.find((a) => a.textContent!.includes('invalid'))!.getAttribute('aria-current')).toBe('true');
    expect(page.querySelector('[data-testid=vote-counts]')!.textContent).toContain('MA/2024/001');
    const table = page.querySelector('[data-testid=rows]')!.textContent!;
    expect(table).toContain('MA/2024/001,79,2024-01-10,A,FRN,X');
    expect(table).toContain('Unknown country code FRN.');
  });

  it('lists the conflicts the import opened with their rows', async () => {
    const harness = await RouterTestingHarness.create('/imports/7');
    http.expectOne('/api/imports/7').flush({
      ...importDto('running'),
      conflictsOpened: [
        { conflictId: 31, vote: { voteId: 41, assemblyVoteId: '101' }, subject: 'details', stateCode: null, rows: 2 },
        { conflictId: 32, vote: { voteId: 41, assemblyVoteId: '101' }, subject: 'ballot', stateCode: 'BRA', rows: 1 },
      ],
    });
    await harness.fixture.whenStable();

    const items = [...harness.routeNativeElement!.querySelectorAll('[data-testid=conflicts-opened] li')];
    expect(items.map((li) => li.textContent!.replace(/\s+/g, ' ').trim())).toEqual([
      'Conflict #31 101 · vote details · 2 rows',
      'Conflict #32 101 · BRA ballot · 1 row',
    ]);
    expect(items[0].querySelector('a')!.getAttribute('href')).toBe('/review/31');
  });
});
