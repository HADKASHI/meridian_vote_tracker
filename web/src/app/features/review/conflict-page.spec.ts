import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { ConflictDecisionDto, ConflictDto } from '../../api/model';
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

  let harness: RouterTestingHarness;

  async function open(body: ConflictDto, list = [listItem({ id: body.id })]): Promise<HTMLElement> {
    harness = await RouterTestingHarness.create(`/review/${body.id}`);
    TestBed.tick();
    http.expectOne((r) => r.url === '/api/conflicts').flush(page(list));
    http.expectOne(`/api/conflicts/${body.id}`).flush(body);
    await harness.fixture.whenStable();
    return harness.routeNativeElement!.querySelector('[data-testid="decision-pane"]')!;
  }

  async function settle(): Promise<void> {
    TestBed.tick();
    await harness.fixture.whenStable();
  }

  function button(pane: HTMLElement, label: string): HTMLButtonElement {
    const found = [...pane.querySelectorAll('button')].find((b) => b.textContent?.trim() === label);
    if (!found) throw new Error(`No button "${label}"`);
    return found;
  }

  const accepted: ConflictDecisionDto = {
    id: 51, conflictId: 7, decision: 'accept', decidedBy: 'dana', decidedAt: '2024-04-03T09:15:00Z',
    comment: 'Assembly confirmed', from: { ballot: 'y', session: null, voteDate: null, title: null },
    to: { ballot: 'a', session: null, voteDate: null, title: null },
  };

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

  it('accepts with a comment, shows the decision, reloads the inbox and moves to the next conflict', async () => {
    const pane = await open(conflict(), [listItem({ id: 6 }), listItem({ id: 7 }), listItem({ id: 8 }), listItem({ id: 9 })]);

    const comment = pane.querySelector<HTMLTextAreaElement>('textarea')!;
    comment.value = 'Assembly confirmed';
    comment.dispatchEvent(new Event('input'));
    button(pane, 'Accept incoming').click();
    await settle();

    const post = http.expectOne({ method: 'POST', url: '/api/conflicts/7/decision' });
    expect(post.request.body).toEqual({ decision: 'accept', comment: 'Assembly confirmed' });
    post.flush(accepted);
    TestBed.tick();
    http.expectOne('/api/conflicts/7').flush(conflict({ decision: accepted }));
    http.expectOne((r) => r.url === '/api/conflicts').flush(page([listItem({ id: 6 }), listItem({ id: 8 }), listItem({ id: 9 })]));
    await settle();

    expect(text(pane, 'decision')).toContain('Accepted by dana');
    expect(pane.querySelector('textarea')).toBeNull();
    button(pane, 'Next conflict').click();
    await vi.waitFor(() => expect(TestBed.inject(Router).url).toBe('/review/8?state=pending'));
  });

  it('keeps the current value with no comment', async () => {
    const pane = await open(conflict());

    button(pane, 'Keep current').click();
    await settle();

    const post = http.expectOne({ method: 'POST', url: '/api/conflicts/7/decision' });
    expect(post.request.body).toEqual({ decision: 'reject', comment: null });
  });

  it('puts a validation message on the comment field', async () => {
    const pane = await open(conflict());

    button(pane, 'Keep current').click();
    await settle();
    http.expectOne({ method: 'POST', url: '/api/conflicts/7/decision' }).flush(
      { type: 'validation', title: 'Invalid', status: 400, traceId: 't1', errors: { comment: ['Keep the comment under 2,000 characters.'] } },
      { status: 400, statusText: 'Bad Request' },
    );
    await settle();

    expect(text(pane, 'comment-error')).toBe('Keep the comment under 2,000 characters.');
    expect(button(pane, 'Keep current').disabled).toBe(false);
  });
});
