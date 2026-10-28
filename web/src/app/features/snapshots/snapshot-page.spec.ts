import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { JobDto, SnapshotDto } from '../../api/model';
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

  async function open(body: SnapshotDto): Promise<HTMLElement> {
    const fixture = TestBed.createComponent(SnapshotPage);
    fixture.componentRef.setInput('snapshotId', '42');
    TestBed.tick();
    http.expectOne('/api/snapshots/42').flush(body);
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  it('shows scores to one decimal with their basis, and n/a for nothing countable', async () => {
    const page = await open(snapshot);

    const rows = [...page.querySelectorAll('[data-testid="ranking"] tr')].map((r) =>
      [...r.querySelectorAll('td')].map((c) => c.textContent?.trim()),
    );
    expect(rows[1]).toEqual(['1', 'Argentina (ARG)', '66.7', '2 countable votes, 2 of 3 weight matched']);
    expect(rows[2]).toEqual(['–', 'Japan (JPN)', 'n/a', 'nothing countable']);
    expect(page.querySelector('[data-testid="snapshot-meta"]')?.textContent).toContain(
      '2024-01-01 → 2024-03-31, minus 1 removed vote',
    );
  });

  it('shows a failed job with its error and no ranking', async () => {
    const page = await open({
      ...snapshot, results: [], voteCount: 0,
      job: { ...job, state: 'failed', lastError: 'MA/2024/041 (first seen in import #13) has no position' },
    });

    expect(page.querySelector('[data-testid="snapshot-job"]')?.textContent).toContain('Failed: MA/2024/041');
    expect(page.querySelector('[data-testid="ranking"]')).toBeNull();
  });
});
