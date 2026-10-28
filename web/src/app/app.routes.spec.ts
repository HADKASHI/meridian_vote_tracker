import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { routes } from './app.routes';

describe('routes', () => {
  beforeEach(() =>
    TestBed.configureTestingModule({
      providers: [provideRouter(routes, withComponentInputBinding()), provideHttpClient(), provideHttpClientTesting()],
    }),
  );

  const screens: [url: string, heading: string, detail?: string][] = [
    ['/imports', 'Imports'],
    ['/imports/12', 'Import report', 'Import #12'],
    ['/review', 'Review'],
    ['/review/7', 'Conflict', 'Conflict #7'],
    ['/votes', 'Votes'],
    ['/votes/101?tab=timeline', 'Vote', 'Vote #101 · timeline'],
    ['/positions', 'Positions'],
    ['/snapshots', 'Snapshots'],
    ['/snapshots/new', 'New snapshot'],
    ['/snapshots/compare?a=1&b=2', 'Compare snapshots'],
    ['/snapshots/3', 'Snapshot', 'Snapshot #3'],
    ['/publications', 'Publications'],
    ['/jobs', 'Jobs'],
    ['/no/such/page', 'Page not found'],
  ];

  it.each(screens)('%s shows its page', async (url, heading, detail) => {
    const harness = await RouterTestingHarness.create();

    await harness.navigateByUrl(url);

    expect(harness.routeNativeElement?.querySelector('h1')?.textContent).toBe(heading);
    if (detail) expect(harness.routeNativeElement?.textContent).toContain(detail);
  });

  it('opens on the Review inbox', async () => {
    const harness = await RouterTestingHarness.create();

    await harness.navigateByUrl('/');

    expect(harness.routeNativeElement?.querySelector('h1')?.textContent).toBe('Review');
  });
});
