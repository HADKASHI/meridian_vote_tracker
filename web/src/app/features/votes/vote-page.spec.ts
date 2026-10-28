import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { OriginDto, VoteDto } from '../../api/model';
import { VotePage } from './vote-page';

const fromFile: OriginDto = {
  kind: 'import_row', importId: 4, fileName: 'q1.csv', lineNo: 17, importRowId: 90, uploadedBy: 'omar',
  uploadedAt: '2024-03-01T09:30:00Z', decisionId: null, conflictId: null, decidedBy: null, decidedAt: null,
};
const fromDecision: OriginDto = {
  kind: 'decision', importId: null, fileName: null, lineNo: null, importRowId: null, uploadedBy: null,
  uploadedAt: null, decisionId: 31, conflictId: 30, decidedBy: 'dana', decidedAt: '2024-03-02T10:00:00Z',
};
const vote: VoteDto = {
  id: 12, assemblyVoteId: 'MA/2024/001', pendingConflicts: 1,
  details: { revisionId: 5, session: 79, voteDate: '2024-01-15', title: 'Ocean Treaty', origin: fromFile },
  position: { revisionId: 8, stance: 'no', weight: 3, setBy: 'lena', setAt: '2024-03-03T00:00:00Z' },
  countryVotes: [
    { stateCode: 'BRA', stateName: 'Brazil', ballot: 'a', revisionId: 33, origin: fromDecision, memberOnVoteDate: true },
    { stateCode: 'FRA', stateName: 'France', ballot: null, revisionId: null, origin: null, memberOnVoteDate: true },
    { stateCode: 'NZL', stateName: 'New Zealand', ballot: 'n', revisionId: 34, origin: fromFile, memberOnVoteDate: false },
  ],
};

describe('VotePage', () => {
  async function render(voteId: string, respond: (http: HttpTestingController) => void): Promise<HTMLElement> {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
    const fixture = TestBed.createComponent(VotePage);
    fixture.componentRef.setInput('voteId', voteId);
    fixture.detectChanges();
    respond(TestBed.inject(HttpTestingController));
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  it('shows the approved details, the position and each value with its origin', async () => {
    const page = await render('12', (http) => http.expectOne('/api/votes/12').flush(vote));

    const details = page.querySelector('[data-testid="vote-details"]')!.textContent!;
    expect(details).toContain('Ocean Treaty');
    expect(details).toContain('q1.csv, line 17, uploaded by omar');
    expect(page.querySelector('[data-testid="vote-position"]')!.textContent).toContain('No · weight 3');
    expect(page.querySelector('[data-testid="vote-pending"]')!.textContent).toContain('1 pending conflict');

    const rows = [...page.querySelectorAll('[data-testid="country-votes"] tr[mat-row]')].map((r) => r.textContent!);
    expect(rows[0]).toContain('A (abstain)');
    expect(rows[0]).toContain('Decision on conflict #30 by dana');
    expect(rows[1]).toContain('No approved value yet');
    expect(rows[2]).toContain('not a member on the vote date');
    expect(page.querySelector('a[href="/imports/4"]')).not.toBeNull();
    expect(page.querySelector('a[href="/review/30"]')).not.toBeNull();
  });

  it('opens on the timeline when the URL asks for it', async () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
    const fixture = TestBed.createComponent(VotePage);
    fixture.componentRef.setInput('voteId', '12');
    fixture.componentRef.setInput('tab', 'timeline');
    fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne('/api/votes/12').flush(vote);

    // The Timeline tab renders lazily, so its first page is requested only once it is the selected tab.
    await vi.waitFor(() => http.expectOne((r) => r.url === '/api/votes/12/timeline'));
  });

  it('says so when the vote does not exist', async () => {
    const page = await render('99', (http) =>
      http.expectOne('/api/votes/99').flush({ type: 'not-found' }, { status: 404, statusText: 'Not Found' }),
    );

    expect(page.querySelector('h1')!.textContent).toContain('Vote not found');
  });
});
