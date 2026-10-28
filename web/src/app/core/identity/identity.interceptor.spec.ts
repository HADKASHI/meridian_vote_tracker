import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationRef } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TAB_USER_KEY } from './current-user';
import { identityInterceptor, USER_HEADER } from './identity.interceptor';

describe('identityInterceptor', () => {
  beforeEach(() => {
    sessionStorage.clear();
    localStorage.clear();
    sessionStorage.setItem(TAB_USER_KEY, 'omar');
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([identityInterceptor])), provideHttpClientTesting()],
    });
  });

  it('sends the tab user with every /api/ call', () => {
    TestBed.inject(HttpClient).get('/api/votes').subscribe();

    const request = TestBed.inject(HttpTestingController).expectOne('/api/votes');
    expect(request.request.headers.get(USER_HEADER)).toBe('omar');
  });

  it('holds a call until the user list says who a fresh tab is', async () => {
    sessionStorage.clear();
    const http = TestBed.inject(HttpTestingController);
    TestBed.inject(HttpClient).get('/api/status').subscribe();
    TestBed.tick();

    http.expectNone('/api/status');
    http.expectOne('/api/users').flush([{ id: 'dana', displayName: 'Dana', role: 'analyst' }]);
    await TestBed.inject(ApplicationRef).whenStable();

    expect(http.expectOne('/api/status').request.headers.get(USER_HEADER)).toBe('dana');
  });

  it('lets a held call go without a user when the user list fails, so its error shows', async () => {
    sessionStorage.clear();
    const http = TestBed.inject(HttpTestingController);
    let failed = false;
    TestBed.inject(HttpClient).get('/api/status').subscribe({ error: () => (failed = true) });
    TestBed.tick();

    http.expectOne('/api/users').flush(null, { status: 503, statusText: 'Unavailable' });
    await TestBed.inject(ApplicationRef).whenStable();
    const held = http.expectOne('/api/status');
    held.flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(held.request.headers.has(USER_HEADER)).toBe(false);
    expect(failed).toBe(true);
  });

  it('leaves calls outside /api/ alone', () => {
    TestBed.inject(HttpClient).get('/assets/logo.svg').subscribe();

    const request = TestBed.inject(HttpTestingController).expectOne('/assets/logo.svg');
    expect(request.request.headers.has(USER_HEADER)).toBe(false);
  });
});
