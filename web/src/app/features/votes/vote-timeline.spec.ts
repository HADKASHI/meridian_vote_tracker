import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { TimelineEventDto } from '../../api/model';
import { describeEvent } from './timeline-labels';
import { VoteTimeline } from './vote-timeline';

const none = {
  stateCode: null,
  from: null,
  to: null,
  origin: null,
  importId: null,
  fileName: null,
  counts: null,
  conflictId: null,
  subject: null,
  decision: null,
  comment: null,
  snapshotId: null,
  snapshotName: null,
  publicationId: null,
  quarter: null,
  reason: null,
  decidedBy: null,
  supersedesPublicationId: null,
} satisfies Partial<TimelineEventDto>;

function event(historyId: number, fields: Partial<TimelineEventDto>): TimelineEventDto {
  return {
    historyId,
    kind: 'import',
    actor: 'dana',
    at: '2024-03-01T09:00:00Z',
    ...none,
    ...fields,
  };
}

const value = {
  ballot: null,
  session: null,
  voteDate: null,
  title: null,
  stance: null,
  weight: null,
};

describe('describeEvent', () => {
  it('shows each approved value change as from → to', () => {
    expect(
      describeEvent(
        event(1, { kind: 'country_vote_revision', stateCode: 'BRA', from: { ...value, ballot: 'n' }, to: { ...value, ballot: 'a' } }),
      ),
    ).toBe('BRA country vote changed: N (no) → A (abstain)');
    expect(
      describeEvent(
        event(2, {
          kind: 'details_revision',
          from: { ...value, session: 79, voteDate: '2024-03-01', title: 'Ocean Treaty' },
          to: { ...value, session: 79, voteDate: '2024-03-04', title: 'Ocean Treaty' },
        }),
      ),
    ).toBe('Vote details changed: session 79, 2024-03-01, “Ocean Treaty” → session 79, 2024-03-04, “Ocean Treaty”');
    expect(
      describeEvent(
        event(3, { kind: 'position_revision', from: { ...value, stance: 'yes', weight: 2 }, to: { ...value, stance: 'no', weight: 5 } }),
      ),
    ).toBe('Position changed: Yes · weight 2 → No · weight 5');
  });

  it('describes every event kind in the glossary words', () => {
    expect(
      describeEvent(
        event(1, {
          kind: 'import',
          importId: 4,
          fileName: 'q1.csv',
          counts: { new: 3, unchanged: 0, duplicate: 1, invalid: 0, contradiction: 0, conflict: 2 },
        }),
      ),
    ).toBe('Import #4 (q1.csv): 3 new, 1 duplicate, 2 conflict');
    expect(
      describeEvent(
        event(2, {
          kind: 'details_revision',
          to: { ...value, session: 79, voteDate: '2024-01-15', title: 'Ocean Treaty' },
        }),
      ),
    ).toBe('Vote details approved: session 79, 2024-01-15, “Ocean Treaty”');
    expect(
      describeEvent(
        event(3, {
          kind: 'country_vote_revision',
          stateCode: 'BRA',
          to: { ...value, ballot: 'a' },
        }),
      ),
    ).toBe('BRA country vote approved: A (abstain)');
    expect(
      describeEvent(
        event(4, { kind: 'position_revision', to: { ...value, stance: 'yes', weight: 4 } }),
      ),
    ).toBe('Position set: Yes · weight 4');
    expect(
      describeEvent(
        event(5, {
          kind: 'conflict_opened',
          conflictId: 9,
          subject: 'ballot',
          stateCode: 'BRA',
          importId: 6,
          fileName: 'rev2.csv',
          to: { ...value, ballot: 'n' },
        }),
      ),
    ).toBe('Conflict #9 opened on the BRA country vote by import #6 (rev2.csv): incoming N (no)');
    expect(
      describeEvent(
        event(6, {
          kind: 'conflict_decided',
          conflictId: 9,
          subject: 'details',
          decision: 'reject',
        }),
      ),
    ).toBe('Conflict #9 on the vote details rejected (current value kept)');
    expect(
      describeEvent(
        event(7, {
          kind: 'decision_not_applied',
          conflictId: 9,
          subject: 'ballot',
          stateCode: 'BRA',
          decision: 'accept',
          decidedBy: 'dana',
        }),
      ),
    ).toBe('Decision to accept conflict #9 not applied: already decided by dana');
    expect(describeEvent(event(8, { kind: 'snapshot', snapshotId: 2, snapshotName: 'Q1' }))).toBe(
      'Used in snapshot “Q1”',
    );
    expect(
      describeEvent(
        event(9, {
          kind: 'publication',
          snapshotName: 'Q1',
          quarter: '2024-Q1',
          reason: 'Fixed data',
          supersedesPublicationId: 41,
        }),
      ),
    ).toBe('Snapshot “Q1” published for 2024-Q1, replacing publication #41: Fixed data');
  });
});

describe('VoteTimeline', () => {
  it('shows the newest page and loads older pages on demand', async () => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    const fixture = TestBed.createComponent(VoteTimeline);
    fixture.componentRef.setInput('voteId', 12);
    fixture.componentRef.setInput('pageSize', 2);
    fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    const element = fixture.nativeElement as HTMLElement;

    const first = http.expectOne((r) => r.url === '/api/votes/12/timeline');
    expect(first.request.params.has('before')).toBe(false);
    first.flush({
      items: [
        event(30, { kind: 'snapshot', snapshotId: 2, snapshotName: 'Q1', actor: 'omar' }),
        event(20, {
          kind: 'conflict_decided',
          conflictId: 9,
          subject: 'ballot',
          stateCode: 'BRA',
          decision: 'accept',
          comment: 'Confirmed',
        }),
      ],
      nextBefore: 20,
    });
    await fixture.whenStable();
    expect(element.querySelectorAll('[data-testid="timeline"] li')).toHaveLength(2);
    expect(element.textContent).toContain('“Confirmed”');

    element.querySelector<HTMLButtonElement>('button')!.click();
    fixture.detectChanges();
    const older = http.expectOne((r) => r.url === '/api/votes/12/timeline');
    expect(older.request.params.get('before')).toBe('20');
    older.flush({
      items: [event(10, { importId: 4, fileName: 'q1.csv', counts: null })],
      nextBefore: null,
    });
    await fixture.whenStable();

    const kinds = [...element.querySelectorAll('[data-testid="timeline"] li')].map((li) =>
      li.getAttribute('data-kind'),
    );
    expect(kinds).toEqual(['snapshot', 'conflict_decided', 'import']);
    expect(element.querySelector('button')).toBeNull();
  });
});
