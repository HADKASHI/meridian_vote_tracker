import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { PageDtoOfVoteSummaryDto } from '../../api/model';
import { searchParams, VotesPage } from './votes-page';

describe('searchParams', () => {
  it('reads the filters and page from the query params and leaves empty ones out', () => {
    expect(searchParams({ q: ' ocean ', session: '79', from: '2024-01-01', noPosition: 'true', page: '2' }, 50)).toEqual({
      q: 'ocean', session: 79, from: '2024-01-01', noPosition: true, page: 2, pageSize: 50,
    });
    expect(searchParams({ q: '  ', session: 'x', noPosition: 'false', page: '0' }, 50)).toEqual({ page: 1, pageSize: 50 });
  });
});

describe('VotesPage', () => {
  it('searches with the URL filters and links each vote to its page', async () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
    const fixture = TestBed.createComponent(VotesPage);
    fixture.componentRef.setInput('q', 'ocean');
    fixture.componentRef.setInput('noPosition', 'true');
    fixture.detectChanges();

    const http = TestBed.inject(HttpTestingController);
    const request = http.expectOne((r) => r.url === '/api/votes');
    expect(request.request.params.get('q')).toBe('ocean');
    expect(request.request.params.get('noPosition')).toBe('true');
    const page: PageDtoOfVoteSummaryDto = {
      items: [
        { id: 12, assemblyVoteId: 'MA/2024/001', session: 79, voteDate: '2024-01-15', title: 'Ocean Treaty',
          position: { revisionId: 3, stance: 'yes', weight: 4, setBy: 'lena', setAt: '2024-02-01T00:00:00Z' } },
      ],
      total: 1, page: 1, pageSize: 50,
    };
    request.flush(page);
    await fixture.whenStable();

    const table = (fixture.nativeElement as HTMLElement).querySelector('[data-testid="votes-table"]')!;
    expect(table.querySelector('a')?.getAttribute('href')).toBe('/votes/12');
    expect(table.textContent).toContain('Ocean Treaty');
    expect(table.textContent).toContain('Yes · weight 4');
  });
});
