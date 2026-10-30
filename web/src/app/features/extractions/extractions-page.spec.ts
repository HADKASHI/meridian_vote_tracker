import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { flushReviewTabs } from '../../shared/ui/review-counts.testing';
import extractionsRoutes from './extractions.routes';
import { extraction, page, summary } from './extractions.testing';

describe('Extractions tab', () => {
  let http: HttpTestingController;

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

  async function open(url: string): Promise<RouterTestingHarness> {
    const harness = await RouterTestingHarness.create(url);
    TestBed.tick();
    return harness;
  }

  /** The list's own request, not the tab count's (which asks for one sheet with a status). */
  const listRequest = (r: { url: string; params: { get(name: string): string | null } }) =>
    r.url === '/api/extractions' && r.params.get('pageSize') === '200';

  /** The lazy route's component loads asynchronously, so its first request is waited for. */
  async function requestTo(url: string) {
    for (let attempt = 0; attempt < 100; attempt++) {
      const [found] = http.match(url);
      if (found) return found;
      await new Promise((resolve) => setTimeout(resolve, 10));
      TestBed.tick();
    }
    return http.expectOne(url);
  }

  function items(harness: RouterTestingHarness): string[] {
    return [...harness.routeNativeElement!.querySelectorAll('[data-testid="extraction-item"]')].map((li) =>
      [...li.querySelectorAll('span')].map((s) => s.textContent!.trim()).join(' '),
    );
  }

  it('lists what is still open with its status, vote, blocking issues and warnings, linking to the sheet', async () => {
    const harness = await open('/review/extractions');

    flushReviewTabs(http);
    http.expectOne(listRequest).flush(page([
      summary({ id: 4, fileName: 'second.pdf', blockingIssues: 2, warnings: 1 }),
      summary({ id: 3, status: 'extracting', voteId: null, job: { ...summary().job, state: 'running' } }),
      summary({ id: 2, fileName: 'old.pdf', status: 'approved' }),
    ]));
    await harness.fixture.whenStable();

    expect(items(harness)).toEqual([
      'second.pdf Needs review MA/2011/042 2 blocking 1 warning dana',
      'MA_2011_042.pdf Reading not read yet dana',
    ]);
    const link = harness.routeNativeElement!.querySelector<HTMLAnchorElement>('[data-testid="extraction-item"] a');
    expect(link?.getAttribute('href')).toBe('/review/extractions/4?state=open');
  });

  it('shows the decided sheets when the filter says so, and every one under all', async () => {
    const decided = await open('/review/extractions?state=decided');
    flushReviewTabs(http);
    http.expectOne(listRequest).flush(page([summary({ id: 4 }), summary({ id: 2, fileName: 'old.pdf', status: 'approved' })]));
    await decided.fixture.whenStable();

    expect(items(decided)).toEqual(['old.pdf Approved MA/2011/042 dana']);
    const current = decided.routeNativeElement!.querySelectorAll('nav[aria-label="Which extractions"] [aria-current="true"]');
    expect([...current].map((a) => a.textContent!.trim())).toEqual(['Decided']);
  });

  it('says so when nothing is open', async () => {
    const harness = await open('/review/extractions');

    flushReviewTabs(http);
    http.expectOne(listRequest).flush(page([]));
    await harness.fixture.whenStable();

    expect(harness.routeNativeElement!.textContent).toContain('No sheets waiting for you.');
  });

  it('shows the Conflicts and Extractions tabs with what waits in each, the open one marked', async () => {
    const harness = await open('/review/extractions');

    http.expectOne(listRequest).flush(page([]));
    flushReviewTabs(http, { conflicts: 3, extractions: 2 });
    await harness.fixture.whenStable();
    TestBed.tick();

    const root = harness.routeNativeElement!;
    expect(root.querySelector('[data-testid="conflicts-count"]')?.textContent).toBe('3');
    expect(root.querySelector('[data-testid="extractions-count"]')?.textContent).toBe('2');
    const current = root.querySelectorAll('nav[aria-label="Review inbox"] [aria-current="page"]');
    expect([...current].map((a) => a.getAttribute('href'))).toEqual(['/review/extractions']);
  });

  it('uploads the chosen PDF and opens the new sheet', async () => {
    const harness = await open('/review/extractions');
    http.expectOne(listRequest).flush(page([]));
    flushReviewTabs(http);
    await harness.fixture.whenStable();
    const picker = harness.routeNativeElement!.querySelector<HTMLInputElement>('input[type="file"]')!;
    const file = new File(['%PDF-1.4'], 'MA_2011_042.pdf', { type: 'application/pdf' });
    Object.defineProperty(picker, 'files', { value: [file] });

    picker.dispatchEvent(new Event('change'));

    const upload = http.expectOne((r) => r.method === 'POST' && r.url === '/api/extractions');
    expect((upload.request.body as FormData).get('file')).toBe(file);
    upload.flush({ extractionId: 9, jobId: 11, existing: false }, { status: 202, statusText: 'Accepted' });
    // The list and the counts are read again, and the new sheet opens beside the list.
    TestBed.tick();
    http.expectOne(listRequest).flush(page([summary({ id: 9, status: 'extracting', voteId: null })]));
    flushReviewTabs(http, { extractions: 1 });
    (await requestTo('/api/extractions/9')).flush(extraction({ id: 9, status: 'extracting', header: null, rows: [], counted: null, printed: null, canApprove: false }));
    await harness.fixture.whenStable();
    expect(TestBed.inject(Router).url).toBe('/review/extractions/9?state=open');
  });

  it('shows the refusal when the upload is turned down and does not open anything', async () => {
    const harness = await open('/review/extractions');
    http.expectOne(listRequest).flush(page([]));
    flushReviewTabs(http);
    await harness.fixture.whenStable();
    const picker = harness.routeNativeElement!.querySelector<HTMLInputElement>('input[type="file"]')!;
    Object.defineProperty(picker, 'files', { value: [new File(['x'], 'big.pdf')] });

    picker.dispatchEvent(new Event('change'));
    http.expectOne((r) => r.method === 'POST' && r.url === '/api/extractions').flush(
      { type: 'payload-too-large', title: 'The file is too large', status: 413, detail: 'Files up to 30 MB can be read.', traceId: 't1' },
      { status: 413, statusText: 'Payload Too Large' },
    );
    await harness.fixture.whenStable();

    expect(harness.routeNativeElement!.textContent).toContain('Files up to 30 MB can be read.');
  });
});
