import { TestBed } from '@angular/core/testing';
import { LoadState } from './load-state';

describe('LoadState', () => {
  it('announces an error as an alert with its message', async () => {
    const fixture = TestBed.createComponent(LoadState);
    fixture.componentRef.setInput('kind', 'error');
    fixture.componentRef.setInput('message', 'The imports could not be loaded.');
    await fixture.whenStable();

    expect((fixture.nativeElement as HTMLElement).querySelector('[role="alert"]')?.textContent?.trim()).toBe(
      'The imports could not be loaded.',
    );
  });

  it('shows an empty state as its message and a loading state as a labelled progress bar', async () => {
    const fixture = TestBed.createComponent(LoadState);
    fixture.componentRef.setInput('kind', 'empty');
    fixture.componentRef.setInput('message', 'No imports yet.');
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;
    expect(element.textContent?.trim()).toBe('No imports yet.');

    fixture.componentRef.setInput('kind', 'loading');
    fixture.componentRef.setInput('message', 'Loading imports');
    await fixture.whenStable();
    expect(element.querySelector('mat-progress-bar')?.getAttribute('aria-label')).toBe('Loading imports');
  });
});
