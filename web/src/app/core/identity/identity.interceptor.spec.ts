import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
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

  it('leaves calls outside /api/ alone', () => {
    TestBed.inject(HttpClient).get('/assets/logo.svg').subscribe();

    const request = TestBed.inject(HttpTestingController).expectOne('/assets/logo.svg');
    expect(request.request.headers.has(USER_HEADER)).toBe(false);
  });
});
