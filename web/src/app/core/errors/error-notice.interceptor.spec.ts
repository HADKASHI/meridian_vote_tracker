import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { CurrentUser } from '../identity/current-user';
import { errorNoticeInterceptor } from './error-notice.interceptor';

describe('errorNoticeInterceptor', () => {
  const snackBar = { open: vi.fn() };
  const currentUser = { rejected: vi.fn() };

  beforeEach(() => {
    snackBar.open.mockReset();
    currentUser.rejected.mockReset();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([errorNoticeInterceptor])),
        provideHttpClientTesting(),
        { provide: MatSnackBar, useValue: snackBar },
        { provide: CurrentUser, useValue: currentUser },
      ],
    });
  });

  function fail(status: number, body: object): unknown {
    let error: unknown;
    TestBed.inject(HttpClient).get('/api/votes').subscribe({ error: (e) => (error = e) });
    TestBed.inject(HttpTestingController).expectOne('/api/votes').flush(body, { status, statusText: 'x' });
    return error;
  }

  it('shows an unexpected error with its reference and passes the error on', () => {
    const error = fail(500, { type: 'unexpected', title: 'Something went wrong', status: 500, traceId: '4bf92f3577b34da6' });

    expect(snackBar.open).toHaveBeenCalledWith('Something went wrong. Reference: 4bf92f3577b34da6', 'Dismiss');
    expect(error).toBeTruthy();
  });

  it('opens the user switcher on a 401', () => {
    fail(401, { type: 'unknown-user', title: 'Unknown user', status: 401, traceId: 't' });

    expect(currentUser.rejected).toHaveBeenCalled();
    expect(snackBar.open).not.toHaveBeenCalled();
  });

  it('leaves a 409 to the screen that shows it in place', () => {
    fail(409, { type: 'already-decided', title: 'Already decided', status: 409, traceId: 't' });

    expect(snackBar.open).not.toHaveBeenCalled();
  });
});
