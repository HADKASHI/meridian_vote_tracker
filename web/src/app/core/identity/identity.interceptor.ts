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
 * first calls wait for it instead of going out anonymous and coming back 401. If the list
 * can't load, they go without a user, so their error reaches the screen instead of hanging.
 */
export const identityInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.startsWith('/api/') || req.url === USERS_URL) return next(req);
  const current = inject(CurrentUser);
  const withUser = (user: string) => (user ? next(req.clone({ setHeaders: { [USER_HEADER]: user } })) : next(req));

  const user = current.identityForCalls();
  if (user !== null) return withUser(user);
  return toObservable(current.identityForCalls, { injector: inject(Injector) }).pipe(
    filter((id): id is string => id !== null),
    take(1),
    switchMap(withUser),
  );
};
