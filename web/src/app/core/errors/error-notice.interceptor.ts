import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { tap } from 'rxjs';
import { CurrentUser } from '../identity/current-user';
import { toProblem } from '../../shared/problem/problem';

/**
 * Adds notices only; it never rewrites errors, which reach the caller unchanged. A 401
 * opens the user switcher; a 5xx or network error shows a notice with the reference
 * (trace id) that finds it in the logs. 400s and 409s are the caller's to show in place.
 */
export const errorNoticeInterceptor: HttpInterceptorFn = (req, next) => {
  const snackBar = inject(MatSnackBar);
  const currentUser = inject(CurrentUser);
  return next(req).pipe(
    tap({
      error: (error: unknown) => {
        if (!(error instanceof HttpErrorResponse)) return;
        if (error.status === 401) {
          currentUser.rejected();
        } else if (error.status === 0 || (error.status >= 500 && error.status !== 501)) {
          snackBar.open(unexpectedMessage(toProblem(error)?.traceId), 'Dismiss');
        }
      },
    }),
  );
};

export function unexpectedMessage(traceId: string | undefined): string {
  return traceId ? `Something went wrong. Reference: ${traceId}` : 'The server could not be reached.';
}
