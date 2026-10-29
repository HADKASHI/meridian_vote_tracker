import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { App } from './app';

@Component({ selector: 'app-blank', template: '' })
class Blank {}

describe('App shell', () => {
  it('marks the navigation link of the current page, not by colour alone', async () => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: 'votes', component: Blank }, { path: 'review', component: Blank }]),
      ],
    });
    // The shell polls /api/status forever, so it never settles: render explicitly instead of waiting.
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    await TestBed.inject(Router).navigateByUrl('/votes');
    fixture.detectChanges();

    const nav = (fixture.nativeElement as HTMLElement).querySelector('nav[aria-label="Main"]')!;
    const current = [...nav.querySelectorAll('a[aria-current="page"]')].map((a) => a.textContent?.trim());
    expect(current).toEqual(['Votes']);
  });
});
