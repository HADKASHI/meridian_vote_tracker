import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RuleSetDto, SnapshotPreviewDto, UserDto } from '../../api/model';
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
    const { fixture, page } = await openAs('analyst');
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
    const { fixture, page } = await openAs('analyst');
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

  async function previewReady(page: HTMLElement, fixture: { whenStable(): Promise<unknown> }, name: string): Promise<void> {
    type(page, 'name', name);
    type(page, 'from', '2024-01-01');
    type(page, 'to', '2024-03-31');
    page.querySelector<HTMLButtonElement>('button[type="submit"]')!.click();
    http.expectOne('/api/snapshots/preview').flush(ready);
    await fixture.whenStable();
  }

  it('creates the snapshot on Calculate and opens its page', async () => {
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const { fixture, page } = await openAs('analyst');
    await previewReady(page, fixture, 'Q1 2024 index');

    button('Calculate').click();
    const create = http.expectOne('/api/snapshots');
    expect(create.request.body).toEqual({
      name: 'Q1 2024 index', note: null,
      selection: { from: '2024-01-01', to: '2024-03-31', excludedVoteIds: null },
    });
    create.flush({ snapshotId: 42, jobId: 9 });
    await fixture.whenStable();

    expect(navigate).toHaveBeenCalledWith(['/snapshots', 42]);
  });

  it('puts a missing name on the name field', async () => {
    const { fixture, page } = await openAs('analyst');
    await previewReady(page, fixture, '');

    button('Calculate').click();
    http.expectOne('/api/snapshots').flush(
      { type: 'validation', title: 'Invalid', status: 400, traceId: 't', errors: { name: ['Enter a name.'] } },
      { status: 400, statusText: 'Bad Request' },
    );
    await fixture.whenStable();

    expect(page.querySelector('[data-testid="name-error"]')?.textContent).toContain('Enter a name.');
  });

  const ruleSets: RuleSetDto[] = [
    { id: 'alignment-2026.1', name: 'Alignment 2026.1 (default)', description: 'Abstentions and absences are left out.', weightUnit: 1, isDefault: true },
    { id: 'strict', name: 'Strict', description: 'Abstentions and absences both count as not aligned.', weightUnit: 1, isDefault: false },
  ];
  const users = (role: UserDto['role']): UserDto[] => [{ id: 'u', displayName: 'U', role }];

  async function openAs(role: UserDto['role']) {
    const fixture = TestBed.createComponent(NewSnapshotPage);
    TestBed.tick();
    http.expectOne('/api/users').flush(users(role));
    http.expectOne('/api/rule-sets').flush(ruleSets);
    await fixture.whenStable();
    return { fixture, page: fixture.nativeElement as HTMLElement };
  }

  it('lets the admin choose a rule set and sends the choice', async () => {
    const { fixture, page } = await openAs('admin');
    const select = page.querySelector<HTMLSelectElement>('select[name="ruleSet"]')!;
    expect([...select.options].map((o) => o.textContent?.trim())).toEqual(['Alignment 2026.1 (default)', 'Strict']);
    expect(select.value).toBe('alignment-2026.1');
    select.value = 'strict';
    select.dispatchEvent(new Event('change'));
    await previewReady(page, fixture, 'Strict cut');

    button('Calculate').click();

    expect(http.expectOne('/api/snapshots').request.body).toMatchObject({ name: 'Strict cut', ruleSet: 'strict' });
  });

  it('sends no rule set when the admin keeps the default', async () => {
    const { fixture, page } = await openAs('admin');
    await previewReady(page, fixture, 'Default cut');

    button('Calculate').click();

    expect(http.expectOne('/api/snapshots').request.body).not.toHaveProperty('ruleSet');
  });

  it('shows an analyst the default rule set without a choice', async () => {
    const { page } = await openAs('analyst');

    expect(page.querySelector('select[name="ruleSet"]')).toBeNull();
    expect(page.querySelector('[data-testid="rule-set-default"]')?.textContent).toContain('Alignment 2026.1 (default)');
  });
});
