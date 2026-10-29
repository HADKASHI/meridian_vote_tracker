import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { SnapshotChangesDto } from '../../api/model';
import { ChangesPanel } from './changes-panel';

const vote = (id: number, assembly: string) => ({ voteId: id, assemblyVoteId: assembly });
const empty = { count: 0, votes: [] };
const nothing: SnapshotChangesDto = {
  readAt: '2024-06-01T10:30:00Z',
  positionChanged: empty, countryVotesChanged: empty, detailsChanged: empty,
  nowSelected: empty, noLongerSelected: empty,
};
const changed: SnapshotChangesDto = {
  ...nothing,
  positionChanged: {
    count: 1,
    votes: [{
      vote: vote(1, 'MA/2024/001'), old: { stance: 'yes', weight: 2 }, new: { stance: 'no', weight: 5 },
      changedBy: { kind: 'position', by: 'lena', importId: null, decisionId: null }, at: '2024-05-01T09:00:00Z',
    }],
  },
  countryVotesChanged: {
    count: 2,
    votes: [{
      vote: vote(1, 'MA/2024/001'), stateCode: 'BRA', old: 'n', new: 'y',
      changedBy: { kind: 'decision', by: 'omar', importId: null, decisionId: 7 }, at: '2024-05-02T09:00:00Z',
    }, {
      vote: vote(1, 'MA/2024/001'), stateCode: 'FRA', old: null, new: 'a',
      changedBy: { kind: 'import', by: 'dana', importId: 12, decisionId: null }, at: '2024-05-03T09:00:00Z',
    }],
  },
  detailsChanged: {
    count: 1,
    votes: [{
      vote: vote(2, 'MA/2024/002'), old: { session: 79, voteDate: '2024-02-10', title: 'Space Debris' },
      new: { session: 79, voteDate: '2024-04-02', title: 'Space Debris' },
      changedBy: { kind: 'decision', by: 'omar', importId: null, decisionId: 8 }, at: '2024-05-04T09:00:00Z',
    }],
  },
  nowSelected: {
    count: 2,
    votes: [
      { vote: vote(4, 'MA/2024/004'), details: { session: 80, voteDate: '2024-03-01', title: 'Fisheries' }, position: { stance: 'no', weight: 4 } },
      { vote: vote(5, 'MA/2024/005'), details: { session: 80, voteDate: '2024-03-02', title: 'Whaling' }, position: null },
    ],
  },
  noLongerSelected: {
    count: 1,
    votes: [{
      vote: vote(2, 'MA/2024/002'), old: { session: 79, voteDate: '2024-02-10', title: 'Space Debris' },
      new: { session: 79, voteDate: '2024-04-02', title: 'Space Debris' },
      changedBy: { kind: 'decision', by: 'omar', importId: null, decisionId: 8 }, at: '2024-05-04T09:00:00Z',
    }],
  },
};

describe('ChangesPanel', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
    http = TestBed.inject(HttpTestingController);
  });

  async function open(body: SnapshotChangesDto): Promise<HTMLElement> {
    const fixture = TestBed.createComponent(ChangesPanel);
    fixture.componentRef.setInput('snapshotId', 42);
    TestBed.tick();
    http.expectOne('/api/snapshots/42/changes').flush(body);
    await new Promise((resolve) => setTimeout(resolve));
    TestBed.tick();
    return fixture.nativeElement as HTMLElement;
  }

  const text = (page: HTMLElement, id: string) => page.querySelector(`[data-testid="${id}"]`)?.textContent?.replace(/\s+/g, ' ').trim();

  it('lists each kind with the old and new value and who or what changed it', async () => {
    const page = await open(changed);

    expect(text(page, 'drift-position')).toContain('Positions changed (1)');
    expect(text(page, 'drift-position')).toContain('MA/2024/001 yes, weight 2 → no, weight 5 by lena (set position) on 2024-05-01');
    expect(text(page, 'drift-country-votes')).toContain('Country votes changed (2)');
    expect(text(page, 'drift-country-votes')).toContain('MA/2024/001 BRA N → Y by omar (decision #7) on 2024-05-02');
    expect(text(page, 'drift-country-votes')).toContain('MA/2024/001 FRA none → A by dana (import #12)');
    expect(text(page, 'drift-details')).toContain('MA/2024/002 date 2024-02-10 → 2024-04-02 by omar (decision #8)');
    expect(text(page, 'drift-now-selected')).toContain('Now selected, not in the snapshot (2)');
    expect(text(page, 'drift-now-selected')).toContain('MA/2024/004 Fisheries, 2024-03-01, no, weight 4');
    expect(text(page, 'drift-now-selected')).toContain('MA/2024/005 Whaling, 2024-03-02, no position yet');
    expect(text(page, 'drift-no-longer-selected')).toContain('No longer selected (1)');
    expect(text(page, 'drift-no-longer-selected')).toContain('MA/2024/002 date 2024-02-10 → 2024-04-02 by omar (decision #8)');
  });

  it('links each vote to its page', async () => {
    const page = await open(changed);

    const link = page.querySelector('[data-testid="drift-position"] a');
    expect(link?.getAttribute('href')).toBe('/votes/1');
  });

  it('is labelled live with the time it was read and shows no score or rank', async () => {
    const page = await open(changed);

    expect(text(page, 'drift-live')).toContain('Live');
    expect(text(page, 'drift-live')).toContain('read 2024-06-01 10:30');
    expect(page.textContent).not.toMatch(/score|rank/i);
  });

  it('says plainly when nothing changed', async () => {
    const page = await open(nothing);

    expect(text(page, 'drift-nothing')).toBe('Nothing has changed since this snapshot was calculated.');
    expect(page.querySelector('[data-testid="drift-position"]')).toBeNull();
    expect(text(page, 'drift-live')).toContain('read 2024-06-01 10:30');
  });

  it('shows the real count and says only the first 20 are listed', async () => {
    const votes = Array.from({ length: 20 }, (_, i) => ({ ...changed.positionChanged.votes[0], vote: vote(i + 1, `MA/2024/${i + 1}`) }));

    const page = await open({ ...nothing, positionChanged: { count: 25, votes } });

    expect(text(page, 'drift-position')).toContain('Positions changed (25)');
    expect(text(page, 'drift-position')).toContain('The first 20 are listed.');
  });
});
