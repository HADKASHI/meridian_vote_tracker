import { TestBed } from '@angular/core/testing';
import { ExpandableList } from './expandable-list';

const states = Array.from({ length: 69 }, (_, i) => `S${String(i).padStart(2, '0')}`);

describe('ExpandableList', () => {
  it('shows a short list inline', async () => {
    const fixture = TestBed.createComponent(ExpandableList);
    fixture.componentRef.setInput('items', ['FRA', 'JPN']);
    fixture.componentRef.setInput('noun', 'missing states');
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;

    expect(element.textContent?.trim()).toBe('FRA, JPN');
    expect(element.querySelector('button')).toBeNull();
  });

  it('shows a long list as its count and expands to the items', async () => {
    const fixture = TestBed.createComponent(ExpandableList);
    fixture.componentRef.setInput('items', states);
    fixture.componentRef.setInput('noun', 'missing states');
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;
    const toggle = element.querySelector('button')!;

    expect(toggle.textContent?.trim()).toBe('69 missing states');
    expect(toggle.getAttribute('aria-expanded')).toBe('false');
    expect(element.textContent).not.toContain('S68');

    toggle.click();
    await fixture.whenStable();

    expect(toggle.getAttribute('aria-expanded')).toBe('true');
    expect(element.textContent).toContain('S00');
    expect(element.textContent).toContain('S68');
  });
});
