import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { flushReviewTabs } from '../../shared/ui/review-counts.testing';
import { listItem, page } from './review.testing';
import reviewRoutes from './review.routes';

describe('Review inbox', () => {
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

  async function open(url: string): Promise<RouterTestingHarness> {
    const harness = await RouterTestingHarness.create(url);
    TestBed.tick();
    flushReviewTabs(http);
    return harness;
  }

  function rows(harness: RouterTestingHarness): string[] {
    return [...harness.routeNativeElement!.querySelectorAll('[data-testid="inbox-item"]')].map((row) =>
      [...row.querySelectorAll('span')].map((field) => field.textContent!.trim()).join(' '),
    );
  }

  it('lists pending conflicts with their vote, subject, current → incoming and import', async () => {
    const harness = await open('/review');

    http.expectOne((r) => r.url === '/api/conflicts' && r.params.get('state') === 'pending').flush(page([
      listItem({
        id: 8, subject: 'details', stateCode: null,
        current: { ballot: null, session: 79, voteDate: '2024-01-15', title: 'Ocean Treaty' },
        incoming: { ballot: null, session: 79, voteDate: '2024-01-16', title: 'Ocean Treaty' },
      }),
      listItem(),
    ]));
    await harness.fixture.whenStable();

    expect(rows(harness)).toEqual([
      'MA/2024/101 Vote details date 2024-01-15 → 2024-01-16 Import #2',
      'MA/2024/101 Country vote · BRA Y → A Import #2',
    ]);
    const link = harness.routeNativeElement!.querySelector<HTMLAnchorElement>('[data-testid="inbox-item"] a');
    expect(link?.getAttribute('href')).toBe('/review/8?state=pending');
  });

  it('shows decided conflicts with their decision when the filter says so', async () => {
    const harness = await open('/review?state=decided');

    http.expectOne((r) => r.url === '/api/conflicts' && r.params.get('state') === 'decided')
      .flush(page([listItem({ decision: 'reject' })]));
    await harness.fixture.whenStable();

    expect(rows(harness)).toEqual(['MA/2024/101 Country vote · BRA Y → A Import #2 Rejected']);
    const current = harness.routeNativeElement!.querySelectorAll('nav[aria-label="Which conflicts"] [aria-current="true"]');
    expect([...current].map((a) => a.textContent!.trim())).toEqual(['Decided']);
  });

  it('says so when nothing waits for review', async () => {
    const harness = await open('/review');

    http.expectOne((r) => r.url === '/api/conflicts').flush(page([]));
    await harness.fixture.whenStable();

    expect(harness.routeNativeElement!.textContent).toContain('No conflicts waiting for review.');
  });

  it('narrows to the votes or the import named in the link and keeps that in each item link', async () => {
    const harness = await open('/review?voteIds=101&voteIds=102&importId=2');

    const request = http.expectOne((r) => r.url === '/api/conflicts');
    expect(request.request.params.getAll('voteIds')).toEqual(['101', '102']);
    expect(request.request.params.get('importId')).toBe('2');
    request.flush(page([listItem()]));
    await harness.fixture.whenStable();

    expect(harness.routeNativeElement!.textContent).toContain('Only some conflicts are shown.');
    const link = harness.routeNativeElement!.querySelector<HTMLAnchorElement>('[data-testid="inbox-item"] a');
    expect(link?.getAttribute('href')).toBe('/review/7?state=pending&voteIds=101&voteIds=102&importId=2');
  });
});
