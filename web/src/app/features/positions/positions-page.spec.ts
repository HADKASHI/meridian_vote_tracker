import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { PageDtoOfPositionWorklistItemDto } from '../../api/model';
import { PositionsPage } from './positions-page';

const worklist: PageDtoOfPositionWorklistItemDto = {
  items: [
    { vote: { voteId: 2, assemblyVoteId: 'MA/2024/002' }, session: 79, voteDate: '2024-02-01', title: 'Water', position: null },
    {
      vote: { voteId: 1, assemblyVoteId: 'MA/2024/001' }, session: 79, voteDate: '2024-01-10', title: 'Ocean Treaty',
      position: { revisionId: 11, stance: 'yes', weight: 4, setBy: 'lena', setAt: '2024-03-01T14:02:00Z' },
    },
  ],
  total: 2, page: 1, pageSize: 50,
};

describe('PositionsPage', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
    http = TestBed.inject(HttpTestingController);
  });

  async function render(inputs: Record<string, unknown> = {}) {
    const fixture = TestBed.createComponent(PositionsPage);
    for (const [name, value] of Object.entries(inputs)) fixture.componentRef.setInput(name, value);
    TestBed.tick();
    return fixture;
  }

  it('lists each vote with its position, or that it has none', async () => {
    const fixture = await render();
    http.expectOne((r) => r.url === '/api/positions').flush(worklist);
    await fixture.whenStable();

    const cells = [...fixture.nativeElement.querySelectorAll('[data-testid="position"]')].map((c: Element) => c.textContent?.trim());
    expect(cells[0]).toBe('No position');
    expect(cells[1]).toContain('Yes · weight 4');
    expect(cells[1]).toContain('lena');
  });

  it('asks only for votes without a position when the filter is on', async () => {
    await render({ noPosition: 'true' });

    const request = http.expectOne((r) => r.url === '/api/positions');
    expect(request.request.params.get('noPosition')).toBe('true');
  });
});
