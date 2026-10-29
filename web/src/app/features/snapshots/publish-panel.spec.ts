import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { PublicationDto } from '../../api/model';
import { CurrentUser } from '../../core/identity/current-user';
import { PublishPanel } from './publish-panel';

const theirs: PublicationDto = {
  id: 5, snapshotId: 3, snapshotName: 'First cut', quarter: '2024-Q1', publishedBy: 'lena',
  publishedAt: '2024-04-02T09:00:00Z', reason: null, supersedesPublicationId: null, supersededByPublicationId: null,
};

describe('PublishPanel', () => {
  let http: HttpTestingController;

  function setUp(isAdmin: boolean): HTMLElement {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), { provide: CurrentUser, useValue: { isAdmin: signal(isAdmin) } }],
    });
    http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(PublishPanel);
    fixture.componentRef.setInput('snapshotId', 42);
    TestBed.tick();
    return fixture.nativeElement as HTMLElement;
  }

  const button = (root: HTMLElement, text: string): HTMLButtonElement =>
    [...root.querySelectorAll('button')].find((b) => b.textContent?.trim() === text)!;

  async function settle(): Promise<void> {
    await new Promise((resolve) => setTimeout(resolve));
    TestBed.tick();
  }

  function type(root: HTMLElement, name: string, text: string): void {
    const input = root.querySelector<HTMLInputElement | HTMLTextAreaElement>(`[name="${name}"]`)!;
    input.value = text;
    input.dispatchEvent(new Event('input'));
  }

  it('is disabled for an analyst with the reason "Admin only"', () => {
    const panel = setUp(false);

    expect(button(panel, 'Publish').disabled).toBe(true);
    expect(panel.textContent).toContain('Admin only');
  });

  it("shows the quarter's publication in place of the button on 409, and replaces it with a reason", async () => {
    const panel = setUp(true);
    type(panel, 'quarter', '2024-Q1');
    TestBed.tick();
    button(panel, 'Publish').click();
    http.expectOne('/api/snapshots/42/publications').flush(
      { type: 'quarter-published', title: 'Published', status: 409, traceId: 't1', publication: theirs },
      { status: 409, statusText: 'Conflict' },
    );
    await settle();

    expect(panel.querySelector('[data-testid="quarter-published"]')?.textContent).toContain('First cut');
    expect(button(panel, 'Publish')).toBeUndefined();

    button(panel, 'Replace it').click();
    TestBed.tick();
    type(panel, 'reason', 'Rev2 corrections accepted.');
    button(panel, 'Replace').click();
    const replace = http.expectOne('/api/snapshots/42/publications');
    expect(replace.request.body).toEqual({ quarter: '2024-Q1', supersedesPublicationId: 5, reason: 'Rev2 corrections accepted.' });
  });
});
