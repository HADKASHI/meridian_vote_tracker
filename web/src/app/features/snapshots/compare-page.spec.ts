import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { JobDto, SnapshotComparisonDto, SnapshotSummaryDto } from '../../api/model';
import { ComparePage } from './compare-page';

const job = (state: JobDto['state']): JobDto => ({
  id: 1, kind: 'snapshot', state, importId: null, snapshotId: 1, placeInQueue: null, rowsRead: 0, votesDone: 0,
  votesTotal: 0, attempts: 1, lastError: null, traceId: null, startedBy: 'dana', createdAt: '2024-04-02T09:00:00Z',
  startedAt: null, finishedAt: null,
});
const summary = (id: number, name: string, state: JobDto['state'] = 'done'): SnapshotSummaryDto => ({
  id, name, createdBy: 'dana', createdAt: '2024-04-02T09:00:00Z', voteCount: 2, job: job(state), publications: [],
});
const list = {
  items: [summary(1, 'Q1 index'), summary(2, 'Q2 index'), summary(3, 'Still running', 'running')],
  total: 3, page: 1, pageSize: 200,
};
const comparison: SnapshotComparisonDto = {
  a: { id: 1, name: 'Q1 index', createdAt: '2024-04-02T09:00:00Z' },
  b: { id: 2, name: 'Q2 index', createdAt: '2024-07-02T09:00:00Z' },
  states: [
    { stateCode: 'ARG', stateName: 'Argentina', scoreA: 50, rankA: 3, scoreB: 80.04, rankB: 1, scoreChange: 30.04, rankChange: 2 },
    { stateCode: 'BRA', stateName: 'Brazil', scoreA: 90, rankA: 1, scoreB: 70, rankB: 2, scoreChange: -20, rankChange: -1 },
    { stateCode: 'FRA', stateName: 'France', scoreA: 60, rankA: 2, scoreB: 60, rankB: 3, scoreChange: 0, rankChange: -1 },
    { stateCode: 'JPN', stateName: 'Japan', scoreA: 40, rankA: 4, scoreB: null, rankB: null, scoreChange: null, rankChange: null },
    { stateCode: 'CIV', stateName: 'Côte d’Ivoire', scoreA: null, rankA: null, scoreB: null, rankB: null, scoreChange: null, rankChange: null },
  ],
};

describe('ComparePage', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
    http = TestBed.inject(HttpTestingController);
  });

  async function settle(): Promise<void> {
    await new Promise((resolve) => setTimeout(resolve));
    TestBed.tick();
  }

  async function open(query: { a?: string; b?: string }) {
    const fixture = TestBed.createComponent(ComparePage);
    if (query.a) fixture.componentRef.setInput('a', query.a);
    if (query.b) fixture.componentRef.setInput('b', query.b);
    TestBed.tick();
    http.expectOne((r) => r.url === '/api/snapshots').flush(list);
    await settle();
    return fixture.nativeElement as HTMLElement;
  }

  const rows = (page: HTMLElement) =>
    [...page.querySelectorAll('[data-testid="movement"] tbody tr')].map((r) =>
      [...r.querySelectorAll('td')].map((c) => c.textContent?.replace(/\s+/g, ' ').trim()),
    );

  it('offers only calculated snapshots and shows nothing to compare until both are chosen', async () => {
    const page = await open({ a: '1' });

    const options = [...page.querySelectorAll('select[data-testid="pick-a"] option')].map((o) => o.textContent?.trim());
    expect(options).toEqual(['Choose a snapshot', 'Q1 index', 'Q2 index']);
    expect((page.querySelector('select[data-testid="pick-a"]') as HTMLSelectElement).value).toBe('1');
    expect(page.querySelector('[data-testid="movement"]')).toBeNull();
    http.expectNone('/api/snapshots/compare');
  });

  it("shows each state's rank and score in both snapshots with the movement, in words as well as arrows", async () => {
    const page = await open({ a: '1', b: '2' });
    http.expectOne((r) => r.url === '/api/snapshots/compare' && r.params.get('a') === '1' && r.params.get('b') === '2')
      .flush(comparison);
    await settle();

    expect(rows(page)).toEqual([
      ['Argentina (ARG)', '3 · 50.0', '1 · 80.0', '+30.0', '▲ up 2'],
      ['Brazil (BRA)', '1 · 90.0', '2 · 70.0', '−20.0', '▼ down 1'],
      ['France (FRA)', '2 · 60.0', '3 · 60.0', '0.0', '▼ down 1'],
      ['Japan (JPN)', '4 · 40.0', 'n/a', 'no movement', 'no movement'],
      ['Côte d’Ivoire (CIV)', 'n/a', 'n/a', 'no movement', 'no movement'],
    ]);
  });

  it('puts the choices in the query string', async () => {
    const page = await open({ a: '1' });
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

    const pick = page.querySelector('select[data-testid="pick-b"]') as HTMLSelectElement;
    pick.value = '2';
    pick.dispatchEvent(new Event('change'));

    expect(navigate).toHaveBeenCalledWith([], expect.objectContaining({ queryParams: { b: '2' }, queryParamsHandling: 'merge' }));
  });

  it('explains a snapshot compared with itself instead of showing an empty table', async () => {
    const page = await open({ a: '2', b: '2' });

    expect(page.textContent).toContain('Choose two different snapshots');
    http.expectNone('/api/snapshots/compare');
  });

  it('says so when the comparison cannot be loaded', async () => {
    const page = await open({ a: '1', b: '999' });
    http.expectOne((r) => r.url === '/api/snapshots/compare').flush(
      { type: 'not-found', title: 'No such snapshot', status: 404 }, { status: 404, statusText: 'Not Found' });
    await settle();

    expect(page.querySelector('[role="alert"]')?.textContent).toContain('could not be compared');
    expect(page.querySelector('[data-testid="movement"]')).toBeNull();
  });
});
