import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { ConflictDto } from '../../api/model';
import { conflict, listItem, page } from './review.testing';
import reviewRoutes from './review.routes';

describe('Decision pane', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: 'review', children: reviewRoutes }], withComponentInputBinding()),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  async function open(body: ConflictDto): Promise<HTMLElement> {
    const harness = await RouterTestingHarness.create(`/review/${body.id}`);
    TestBed.tick();
    http.expectOne((r) => r.url === '/api/conflicts').flush(page([listItem({ id: body.id })]));
    http.expectOne(`/api/conflicts/${body.id}`).flush(body);
    await harness.fixture.whenStable();
    return harness.routeNativeElement!.querySelector('[data-testid="decision-pane"]')!;
  }

  function text(pane: HTMLElement, testId: string): string {
    return pane.querySelector(`[data-testid="${testId}"]`)?.textContent?.replace(/\s+/g, ' ').trim() ?? '';
  }

  it('shows both values with the file, line and uploader that brought them', async () => {
    const pane = await open(conflict());

    expect(text(pane, 'current')).toContain('Y');
    expect(text(pane, 'current')).toContain('q1.csv line 12 · uploaded by dana');
    expect(text(pane, 'incoming')).toContain('A');
    expect(text(pane, 'incoming')).toContain('q1-rev2.csv line 88 · uploaded by omar');
    expect(pane.textContent).toContain('MA/2024/101');
    expect(pane.textContent).toContain('Ballot · BRA');
  });

  it('names the decision that approved a current value', async () => {
    const pane = await open(conflict({
      current: {
        value: { ballot: 'n', session: null, voteDate: null, title: null }, revisionId: 31,
        origin: {
          kind: 'decision', importId: null, fileName: null, lineNo: null, importRowId: null, uploadedBy: null, uploadedAt: null,
          decisionId: 50, conflictId: 6, decidedBy: 'omar', decidedAt: '2024-04-03T08:00:00Z',
        },
      },
    }));

    expect(text(pane, 'current')).toContain('approved by omar deciding conflict #6');
  });

  it('keeps a decided conflict viewable with its decision and the attempts not applied', async () => {
    const pane = await open(conflict({
      decision: {
        id: 51, conflictId: 7, decision: 'accept', decidedBy: 'dana', decidedAt: '2024-04-03T09:15:00Z',
        comment: 'Assembly confirmed', from: { ballot: 'y', session: null, voteDate: null, title: null },
        to: { ballot: 'a', session: null, voteDate: null, title: null },
      },
      notApplied: [{ id: 52, attemptedBy: 'omar', attempted: 'reject', attemptedAt: '2024-04-03T09:15:00Z' }],
    }));

    expect(text(pane, 'decision')).toContain('Accepted by dana');
    expect(text(pane, 'decision')).toContain('Y → A');
    expect(text(pane, 'decision')).toContain('Assembly confirmed');
    expect(text(pane, 'not-applied')).toContain('omar tried to reject; not applied');
  });
});
