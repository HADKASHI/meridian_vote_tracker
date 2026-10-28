import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { JobDto, StatusDto } from '../../api/model';
import { PageVisibility } from '../page-visibility';
import { jobsLabel } from './jobs-indicator';
import { ShellStatus } from './shell-status';

const runningJob: JobDto = {
  id: 1, kind: 'import', state: 'running', importId: 7, snapshotId: null, placeInQueue: null,
  rowsRead: 100, votesDone: 3, votesTotal: 40, attempts: 1, lastError: null, traceId: null,
  startedBy: 'dana', createdAt: '2024-01-01T00:00:00Z', startedAt: null, finishedAt: null,
};
const busy: StatusDto = { running: [runningJob], queued: [], recentlyFinished: [], pendingConflicts: 8 };
const idle: StatusDto = { running: [], queued: [], recentlyFinished: [], pendingConflicts: 0 };

describe('ShellStatus', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    vi.useFakeTimers();
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => vi.useRealTimers());

  async function respond(status: StatusDto): Promise<void> {
    http.expectOne('/api/status').flush(status);
    await vi.advanceTimersByTimeAsync(0);
    TestBed.tick();
  }

  async function wait(ms: number): Promise<void> {
    await vi.advanceTimersByTimeAsync(ms);
    TestBed.tick();
  }

  it('polls every second while a job is active, then every five seconds', async () => {
    const shell = TestBed.inject(ShellStatus);
    TestBed.tick();
    await respond(busy);
    expect(shell.pendingConflicts()).toBe(8);
    expect(shell.active()).toBe(true);

    await wait(999);
    http.expectNone('/api/status');
    await wait(1);
    await respond(idle);

    await wait(4999);
    http.expectNone('/api/status');
    await wait(1);
    http.expectOne('/api/status');
  });

  it('stops polling while the tab is hidden', async () => {
    TestBed.inject(ShellStatus);
    TestBed.tick();
    await respond(busy);

    TestBed.inject(PageVisibility).visible.set(false);
    await wait(10_000);

    http.expectNone('/api/status');
  });
});

describe('jobsLabel', () => {
  it('reads like the header indicator', () => {
    expect(jobsLabel(2, 1)).toBe('2 jobs running · 1 queued');
    expect(jobsLabel(1, 0)).toBe('1 job running');
    expect(jobsLabel(0, 0)).toBe('No jobs running');
  });
});
