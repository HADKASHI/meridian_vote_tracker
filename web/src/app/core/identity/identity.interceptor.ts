import { HttpInterceptorFn } from '@angular/common/http';
import { inject, Injector } from '@angular/core';
import { toObservable } from '@angular/core/rxjs-interop';
import { filter, switchMap, take } from 'rxjs';
import { CurrentUser } from './current-user';

export const USER_HEADER = 'X-User-Id';
const USERS_URL = '/api/users';

/**
 * Sends this tab's user with every API call; the generated client and httpResource both
 * pass through here. A fresh tab doesn't know its user until the user list loads, so its
 * first calls wait for it instead of going out anonymous and coming back 401.
 */
export const identityInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.startsWith('/api/') || req.url === USERS_URL) return next(req);
  const current = inject(CurrentUser);
  const withUser = (user: string) => next(req.clone({ setHeaders: { [USER_HEADER]: user } }));

  const user = current.id();
  if (user) return withUser(user);
  return toObservable(current.id, { injector: inject(Injector) }).pipe(
    filter((id): id is string => !!id),
    take(1),
    switchMap(withUser),
  );
};
