import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { CurrentUser } from './current-user';

export const USER_HEADER = 'X-User-Id';

/** Sends this tab's user with every API call; the generated client and httpResource both pass through here. */
export const identityInterceptor: HttpInterceptorFn = (req, next) => {
  const user = inject(CurrentUser).id();
  return req.url.startsWith('/api/') && user ? next(req.clone({ setHeaders: { [USER_HEADER]: user } })) : next(req);
};
