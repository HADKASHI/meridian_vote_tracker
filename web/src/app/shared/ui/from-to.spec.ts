import { TestBed } from '@angular/core/testing';
import { FromTo } from './from-to';

describe('FromTo', () => {
  it('shows both values with an arrow that reads as "to"', async () => {
    const fixture = TestBed.createComponent(FromTo);
    fixture.componentRef.setInput('from', 'Y');
    fixture.componentRef.setInput('to', 'A');
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;

    expect(element.textContent?.replace(/\s+/g, ' ').trim()).toBe('Y → A');
    expect(element.querySelector('[role="img"]')?.getAttribute('aria-label')).toBe('to');
  });
});
