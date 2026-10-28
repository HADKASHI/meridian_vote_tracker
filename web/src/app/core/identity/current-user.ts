import { computed, Injectable, signal } from '@angular/core';
import { listUsersResource } from '../../api/users/users.resource';
import { Role, UserDto } from '../../api/model';
import { readStorage, removeStorage, writeStorage } from '../browser-storage';

export const TAB_USER_KEY = 'mvt.user';
export const LAST_USER_KEY = 'mvt.lastUser';

/**
 * Who this tab acts as. The choice lives in sessionStorage, so each tab keeps its own
 * user across reloads (Dana in one tab, Omar in another); a new tab starts as the last
 * user chosen anywhere, from localStorage. The API still enforces roles (401/403).
 */
@Injectable({ providedIn: 'root' })
export class CurrentUser {
  readonly users = listUsersResource();

  private readonly chosenId = signal<string | null>(
    readStorage(() => sessionStorage, TAB_USER_KEY) ?? readStorage(() => localStorage, LAST_USER_KEY),
  );

  /** Set when the switcher should open itself, e.g. after a 401. */
  readonly switcherRequested = signal(false);

  /** The chosen user, or the first seeded user until the list says otherwise. */
  readonly id = computed(() => {
    const users = this.users.hasValue() ? this.users.value() : [];
    const chosen = this.chosenId();
    if (users.length === 0) return chosen;
    return users.some((u) => u.id === chosen) ? chosen : users[0].id;
  });

  readonly user = computed<UserDto | undefined>(() =>
    this.users.hasValue() ? this.users.value().find((u) => u.id === this.id()) : undefined,
  );

  readonly isAdmin = computed(() => this.user()?.role === Role.admin);

  switchTo(id: string): void {
    this.chosenId.set(id);
    this.switcherRequested.set(false);
    writeStorage(() => sessionStorage, TAB_USER_KEY, id);
    writeStorage(() => localStorage, LAST_USER_KEY, id);
  }

  /** The API refused this tab's user: forget it and ask for another. */
  rejected(): void {
    this.chosenId.set(null);
    removeStorage(() => sessionStorage, TAB_USER_KEY);
    removeStorage(() => localStorage, LAST_USER_KEY);
    this.switcherRequested.set(true);
  }

  /** The user to send, '' when the user list failed (calls then go without one), or null while it loads. */
  readonly identityForCalls = computed(() => this.id() ?? (this.users.status() === 'error' ? '' : null));
}
