import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { JobDto, SnapshotBreakdownDto, SnapshotDto } from '../../api/model';
import { SnapshotPage } from './snapshot-page';

const job: JobDto = {
  id: 9, kind: 'snapshot', state: 'done', importId: null, snapshotId: 42, placeInQueue: null,
  rowsRead: 0, votesDone: 2, votesTotal: 2, attempts: 1, lastError: null, traceId: null,
  startedBy: 'dana', createdAt: '2024-04-02T09:00:00Z', startedAt: null, finishedAt: null,
};
const snapshot: SnapshotDto = {
  id: 42, name: 'Q1 2024 index', note: null, scoringRulesVersion: 'alignment-2026.1',
  selection: { from: '2024-01-01', to: '2024-03-31', excludedVoteIds: [7] },
  createdBy: 'dana', createdAt: '2024-04-02T09:00:00Z', job, voteCount: 2, warnings: [], publications: [],
  results: [
    { stateCode: 'ARG', stateName: 'Argentina', matchedWeight: 2, countedWeight: 3, countableVotes: 2, score: 200 / 3, rank: 1 },
    { stateCode: 'JPN', stateName: 'Japan', matchedWeight: 0, countedWeight: 0, countableVotes: 0, score: null, rank: null },
  ],
};

describe('SnapshotPage', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
    http = TestBed.inject(HttpTestingController);
  });

  async function open(body: SnapshotDto, state?: string): Promise<HTMLElement> {
    const fixture = TestBed.createComponent(SnapshotPage);
    fixture.componentRef.setInput('snapshotId', '42');
    if (state) fixture.componentRef.setInput('state', state);
    TestBed.tick();
    http.expectOne('/api/snapshots/42').flush(body);
    await settle();
    return fixture.nativeElement as HTMLElement;
  }

  /** Lets responses reach the template; whenStable would wait on requests the test answers later. */
  async function settle(): Promise<void> {
    await new Promise((resolve) => setTimeout(resolve));
    TestBed.tick();
  }

  it('shows scores to one decimal with their basis, and n/a for nothing countable', async () => {
    const page = await open(snapshot);

    const rows = [...page.querySelectorAll('[data-testid="ranking"] tr')].map((r) =>
      [...r.querySelectorAll('td')].map((c) => c.textContent?.trim()),
    );
    expect(rows[1]).toEqual(['1', 'Argentina (ARG)', '66.7 2 countable votes · 2 of 3 weight matched']);
    expect(rows[2]).toEqual(['–', 'Japan (JPN)', 'n/a nothing countable']);
    expect(page.querySelector('[data-testid="snapshot-meta"]')?.textContent).toContain(
      '2024-01-01 → 2024-03-31, minus 1 removed vote',
    );
  });

  it('leads to the comparison with this snapshot as A', async () => {
    const page = await open(snapshot);

    expect(page.querySelector('[data-testid="compare-link"]')?.getAttribute('href')).toBe('/snapshots/compare?a=42');
  });

  it('shows a failed job with its error and no ranking', async () => {
    const page = await open({
      ...snapshot, results: [], voteCount: 0,
      job: { ...job, state: 'failed', lastError: 'MA/2024/041 (first seen in import #13) has no position' },
    });

    expect(page.querySelector('[data-testid="snapshot-job"]')?.textContent).toContain('Failed: MA/2024/041');
    expect(page.querySelector('[data-testid="ranking"]')).toBeNull();
  });

  it("opens a state's breakdown with each vote's category and the exact value used", async () => {
    const page = await open(snapshot, 'ARG');
    http.expectOne('/api/snapshots/42/states/ARG').flush({
      result: snapshot.results[0],
      lines: [
        {
          vote: { voteId: 1, assemblyVoteId: 'MA/2024/001' }, voteDate: '2024-01-10', title: 'Ocean Treaty',
          stance: 'yes', weight: 2, ballot: 'y', category: 'match', countryVoteRevisionId: 18,
        },
        {
          vote: { voteId: 2, assemblyVoteId: 'MA/2024/002' }, voteDate: '2024-02-20', title: 'Space Debris',
          stance: 'no', weight: 1, ballot: null, category: 'no_data', countryVoteRevisionId: null,
        },
      ],
    } satisfies SnapshotBreakdownDto);
    await settle();

    const rows = [...page.querySelectorAll('[data-testid="breakdown-lines"] tr')].map((r) =>
      [...r.querySelectorAll('td')].map((c) => c.textContent?.trim()),
    );
    expect(rows[1]).toEqual(['MA/2024/001', '2024-01-10', 'Ocean Treaty', 'yes, weight 2', 'Y', 'match', '18']);
    expect(rows[2]).toEqual(['MA/2024/002', '2024-02-20', 'Space Debris', 'no, weight 1', '–', 'no data', 'none']);
    expect(page.querySelector('[data-testid="breakdown-result"]')?.textContent).toContain('Score 66.7');
  });

  it('asks what changed since it was calculated, but only for a calculated snapshot', async () => {
    await open(snapshot);
    http.expectOne('/api/snapshots/42/changes');

    await open({ ...snapshot, job: { ...job, state: 'running' } });
    http.expectNone('/api/snapshots/42/changes');

    await open({ ...snapshot, job: { ...job, state: 'failed', lastError: 'x' }, results: [], voteCount: 0 });
    http.expectNone('/api/snapshots/42/changes');
  });
});
