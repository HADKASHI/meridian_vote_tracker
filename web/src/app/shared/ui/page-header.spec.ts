import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { PageHeader } from './page-header';

@Component({
  imports: [PageHeader],
  template: `<app-page-header heading="MA/2024/003" [back]="{ label: 'Votes', link: '/votes' }">
    <p pageLead>Session 79</p>
  </app-page-header>`,
})
class Host {}

describe('PageHeader', () => {
  it('shows the page title with a breadcrumb back to the list it came from', async () => {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    const fixture = TestBed.createComponent(Host);
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('h1')?.textContent?.trim()).toBe('MA/2024/003');
    const crumb = element.querySelector('nav[aria-label="Breadcrumb"] a');
    expect(crumb?.getAttribute('href')).toBe('/votes');
    expect(crumb?.textContent).toContain('Votes');
    expect(element.textContent).toContain('Session 79');
  });
});
