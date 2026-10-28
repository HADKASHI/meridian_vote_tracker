import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { SnapshotPreviewDto } from '../../api/model';
import { NewSnapshotPage } from './new-snapshot-page';

const blocked: SnapshotPreviewDto = {
  voteCount: 40,
  noPosition: [
    { voteId: 7, assemblyVoteId: 'MA/2024/007' },
    { voteId: 41, assemblyVoteId: 'MA/2024/041' },
  ],
  warnings: [
    {
      kind: 'import_running', message: 'Import #12 is applying (23 of 40 votes).', vote: null,
      pendingConflicts: null, missingStateCodes: null, importId: 12, votesDone: 23, votesTotal: 40,
    },
  ],
};
const ready: SnapshotPreviewDto = { voteCount: 38, noPosition: [], warnings: [] };

describe('NewSnapshotPage', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    http = TestBed.inject(HttpTestingController);
  });

  function type(root: HTMLElement, name: string, text: string): void {
    const input = root.querySelector<HTMLInputElement>(`[name="${name}"]`)!;
    input.value = text;
    input.dispatchEvent(new Event('input'));
  }

  const overlay = (): HTMLElement => document.querySelector('.cdk-overlay-container') as HTMLElement;
  const button = (text: string): HTMLButtonElement =>
    [...overlay().querySelectorAll('button')].find((b) => b.textContent?.trim() === text)!;

  it('blocks Calculate until the votes without a position are removed, and records them in the selection', async () => {
    const fixture = TestBed.createComponent(NewSnapshotPage);
    const page = fixture.nativeElement as HTMLElement;
    await fixture.whenStable();
    type(page, 'name', 'Q1 2024 index');
    type(page, 'from', '2024-01-01');
    type(page, 'to', '2024-03-31');
    page.querySelector<HTMLButtonElement>('button[type="submit"]')!.click();

    const first = http.expectOne('/api/snapshots/preview');
    expect(first.request.body).toEqual({ from: '2024-01-01', to: '2024-03-31', excludedVoteIds: null });
    first.flush(blocked);
    await fixture.whenStable();

    expect(overlay().querySelector('[data-testid="preview-blocked"]')?.textContent).toContain('MA/2024/041');
    expect(overlay().querySelector('[data-testid="preview-warnings"]')?.textContent).toContain('Import #12');
    expect(button('Calculate').disabled).toBe(true);

    button('Remove these votes').click();
    const second = http.expectOne('/api/snapshots/preview');
    expect(second.request.body).toEqual({ from: '2024-01-01', to: '2024-03-31', excludedVoteIds: [7, 41] });
    second.flush(ready);
    await fixture.whenStable();

    expect(overlay().querySelector('[data-testid="preview-summary"]')?.textContent).toContain(
      '2024-01-01 → 2024-03-31, minus MA/2024/007, MA/2024/041',
    );
    expect(overlay().querySelector('[data-testid="preview-ready"]')).not.toBeNull();
    expect(button('Calculate').disabled).toBe(false);
  });

  it('shows no-votes-match in the form, with its reference', async () => {
    const fixture = TestBed.createComponent(NewSnapshotPage);
    const page = fixture.nativeElement as HTMLElement;
    await fixture.whenStable();
    type(page, 'from', '2023-01-01');
    type(page, 'to', '2023-12-31');
    page.querySelector<HTMLButtonElement>('button[type="submit"]')!.click();

    http.expectOne('/api/snapshots/preview').flush(
      { type: 'no-votes-match', title: 'No votes match', status: 400, detail: 'The selection matches no votes.', traceId: 'abc123' },
      { status: 400, statusText: 'Bad Request' },
    );
    await fixture.whenStable();

    expect(page.querySelector('app-problem-banner')?.textContent).toContain('The selection matches no votes.');
    expect(page.querySelector('app-problem-banner')?.textContent).toContain('abc123');
  });
});
