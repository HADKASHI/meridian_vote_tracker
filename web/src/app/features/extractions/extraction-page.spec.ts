import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { ExtractionDto } from '../../api/model';
import { flushReviewTabs } from '../../shared/ui/review-counts.testing';
import extractionsRoutes from './extractions.routes';
import { blocked, extraction, page, row, summary } from './extractions.testing';

describe('Extraction pane', () => {
  let http: HttpTestingController;
  let harness: RouterTestingHarness;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: 'review/extractions', children: extractionsRoutes }], withComponentInputBinding()),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  const listRequest = (r: { url: string; params: { get(name: string): string | null } }) =>
    r.url === '/api/extractions' && r.params.get('pageSize') === '200';

  /** Opens the sheet beside the list and answers the three calls that fill the page. */
  async function open(body: ExtractionDto): Promise<HTMLElement> {
    harness = await RouterTestingHarness.create(`/review/extractions/${body.id}?state=open`);
    TestBed.tick();
    http.expectOne(listRequest).flush(page([summary({ id: body.id, status: body.status })]));
    flushReviewTabs(http);
    http.expectOne(`/api/extractions/${body.id}`).flush(body);
    await harness.fixture.whenStable();
    return pane();
  }

  const pane = () => harness.routeNativeElement!.querySelector<HTMLElement>('[data-testid="extraction-pane"]')!;
  const byId = (id: string) => pane().querySelector<HTMLElement>(`[data-testid="${id}"]`);
  const button = (id: string) => byId(id) as HTMLButtonElement;

  /** After an action the sheet, the list and the counts are read again. */
  async function reloaded(body: ExtractionDto): Promise<void> {
    TestBed.tick();
    http.expectOne(`/api/extractions/${body.id}`).flush(body);
    http.expectOne(listRequest).flush(page([summary({ id: body.id, status: body.status })]));
    flushReviewTabs(http);
    await harness.fixture.whenStable();
    TestBed.tick();
  }

  function choose(select: HTMLSelectElement, value: string): void {
    select.value = value;
    select.dispatchEvent(new Event('change'));
    TestBed.tick();
  }

  function type(input: HTMLInputElement, value: string): void {
    input.value = value;
    input.dispatchEvent(new Event('input'));
    TestBed.tick();
  }

  it('lines the counts up against the totals the sheet prints and marks the ones that differ', async () => {
    await open(blocked());

    const totals = (id: string) => [...byId(id)!.querySelectorAll('td')].map((td) => td.textContent!.replace(/\s+/g, ' ').trim());
    expect([...byId('totals')!.querySelectorAll('thead th')].map((th) => th.textContent!.trim()))
      .toEqual(['Source', 'Yes', 'No', 'Abstentions', 'Not voting', 'Total']);
    expect(totals('counted')).toEqual(['0 (differs from the printed total) ≠', '1', '0', '0', '1']);
    const inputs = [...byId('printed')!.querySelectorAll<HTMLInputElement>('input')].map((i) => i.value);
    expect(inputs).toEqual(['1', '1', '0', '0']);
  });

  it('lists the blocking issues before the warnings, each saying which it is', async () => {
    await open(blocked());

    const issues = [...byId('issues')!.querySelectorAll('li')].map((li) => li.textContent!.replace(/\s+/g, ' ').trim());
    expect(issues).toEqual([
      'Blocks approval ARG has more than one mark.',
      'Blocks approval Counted Yes 0, No 1 but the sheet prints Yes 1, No 1.',
      'Warning The vote number on the sheet does not match the file name.',
    ]);
  });

  it('keeps Approve disabled while an issue blocks, and says why', async () => {
    await open(blocked());

    expect(button('approve').disabled).toBe(true);
    expect(byId('why-not')!.textContent).toContain('Fix or reject what blocks approval first.');
    expect(button('reject').disabled).toBe(false);
  });

  it('shows the rows with issues, and every row when asked', async () => {
    await open(blocked());
    const codes = () => [...pane().querySelectorAll('[data-testid="row"]')].map((r) => r.getAttribute('data-code'));

    expect(codes()).toEqual(['ARG']);
    expect(pane().querySelector<HTMLSelectElement>('select')!.selectedOptions[0].textContent).toContain('Two marks: choose');

    byId('toggle-rows')!.click();
    TestBed.tick();
    expect(codes()).toEqual(['ARG', 'BRA']);
  });

  it('enables Save when a ballot is chosen, still holds Approve back, and saves only what changed', async () => {
    await open(blocked());
    expect(button('save').disabled).toBe(true);

    choose(pane().querySelector<HTMLSelectElement>('select')!, 'y');

    expect(button('save').disabled).toBe(false);
    expect(button('approve').disabled).toBe(true);
    button('save').click();
    const save = http.expectOne((r) => r.method === 'POST' && r.url === '/api/extractions/3/edits');
    expect(save.request.body).toEqual({ rows: [{ position: 1, code: null, choice: 'y' }], header: null, printed: null });
    save.flush(extraction());
    await reloaded(extraction());

    expect(button('save').disabled).toBe(true);
    expect(button('approve').disabled).toBe(false);
  });

  it('sends a corrected code, header field and printed total, and nothing else', async () => {
    await open(blocked());

    type(pane().querySelector<HTMLInputElement>('input.code')!, ' arx ');
    type(byId('header-date') as HTMLInputElement, '5 October 2011');
    type(byId('printed-yes') as HTMLInputElement, '2');
    button('save').click();

    const save = http.expectOne((r) => r.method === 'POST' && r.url === '/api/extractions/3/edits');
    expect(save.request.body).toEqual({
      rows: [{ position: 1, code: 'ARX', choice: null }],
      header: { voteId: null, session: null, date: '5 October 2011', title: null },
      printed: { yes: 2, no: 1, abstentions: 0, notVoting: 0 },
    });
    save.flush(blocked());
    await reloaded(blocked());
  });

  it('does not send a printed total that is not a whole number', async () => {
    await open(blocked());

    type(byId('printed-yes') as HTMLInputElement, '1.5');
    button('save').click();
    TestBed.tick();

    http.expectNone((r) => r.method === 'POST');
    expect(pane().textContent).toContain('Each printed total must be a whole number, 0 or more.');
  });

  it('approves with the comment and then shows who approved it and the import it made', async () => {
    await open(extraction());
    const comment = pane().querySelector<HTMLTextAreaElement>('textarea')!;
    comment.value = 'Checked against the original.';
    comment.dispatchEvent(new Event('input'));
    TestBed.tick();

    button('approve').click();
    const approve = http.expectOne((r) => r.method === 'POST' && r.url === '/api/extractions/3/approval');
    expect(approve.request.body).toEqual({ comment: 'Checked against the original.' });
    approve.flush({ decision: { status: 'approved', decidedBy: 'omar', decidedAt: '2024-04-02T10:00:00Z', comment: null }, importId: 12 });
    await reloaded(extraction({
      status: 'approved', importId: 12,
      decision: { status: 'approved', decidedBy: 'omar', decidedAt: '2024-04-02T10:00:00Z', comment: 'Checked against the original.' },
    }));

    expect(byId('decision')!.textContent).toContain('Approved by omar');
    expect(byId('import-link')!.getAttribute('href')).toBe('/imports/12');
    expect(byId('approve')).toBeNull();
  });

  it('rejects with the comment', async () => {
    await open(blocked());
    const comment = pane().querySelector<HTMLTextAreaElement>('textarea')!;
    comment.value = 'Not the sheet for this vote.';
    comment.dispatchEvent(new Event('input'));
    TestBed.tick();

    button('reject').click();

    const reject = http.expectOne((r) => r.method === 'POST' && r.url === '/api/extractions/3/rejection');
    expect(reject.request.body).toEqual({ comment: 'Not the sheet for this vote.' });
    reject.flush({ status: 'rejected', decidedBy: 'omar', decidedAt: '2024-04-02T10:00:00Z', comment: 'Not the sheet for this vote.' });
    await reloaded(extraction({
      status: 'rejected', decision: { status: 'rejected', decidedBy: 'omar', decidedAt: '2024-04-02T10:00:00Z', comment: 'Not the sheet for this vote.' },
    }));
    expect(byId('decision')!.textContent).toContain('Rejected by omar');
  });

  it('shows the winner instead of the buttons when someone else decided first, and never retries', async () => {
    await open(extraction());

    button('approve').click();
    http.expectOne('/api/extractions/3/approval').flush(
      {
        type: 'already-decided', title: 'Already decided', status: 409, traceId: 'trace-1',
        decision: { status: 'rejected', decidedBy: 'omar', decidedAt: '2024-04-02T10:00:00Z', comment: 'Wrong sheet.' },
      },
      { status: 409, statusText: 'Conflict' },
    );
    await reloaded(extraction({
      status: 'rejected', decision: { status: 'rejected', decidedBy: 'omar', decidedAt: '2024-04-02T10:00:00Z', comment: 'Wrong sheet.' },
    }));

    const card = byId('already-decided')!;
    expect(card.textContent).toContain('omar rejected this sheet');
    expect(card.textContent).toContain('Wrong sheet.');
    expect(card.textContent).toContain('Reference: trace-1');
    expect(byId('approve')).toBeNull();
    http.expectNone((r) => r.method === 'POST');
  });

  it('shows why a sheet failed and offers to try it again', async () => {
    await open(extraction({ status: 'failed', error: 'The file is not a PDF.', header: null, rows: [], counted: null, printed: null, canApprove: false }));

    expect(byId('failed')!.textContent).toContain('The file is not a PDF.');
    button('retry').click();
    http.expectOne((r) => r.method === 'POST' && r.url === '/api/extractions/3/retry').flush({ extractionId: 3, jobId: 5, existing: false });
    await reloaded(extraction({ status: 'extracting', header: null, rows: [], counted: null, printed: null, canApprove: false }));

    expect(byId('reading')).not.toBeNull();
    expect(byId('failed')).toBeNull();
  });

  it('says it is reading while the job runs, and offers no decision yet', async () => {
    await open(extraction({ status: 'extracting', header: null, rows: [], counted: null, printed: null, canApprove: false }));

    expect(byId('reading')!.textContent).toContain('Reading the sheet');
    expect(byId('approve')).toBeNull();
    expect(byId('reject')).toBeNull();
  });

  it('lists the edits made so far with what each replaced', async () => {
    await open(extraction({
      rows: [row(1, 'ARG')],
      edits: [{ by: 'omar', at: '2024-04-02T09:30:00Z', field: 'choice', position: 1, code: 'ARG', from: null, to: 'Y' }],
    }));

    expect(byId('edits')!.textContent).toContain('omar changed choice of ARG: — → Y');
  });
});
