import { ChangeDetectionStrategy, Component, effect, inject, viewChild } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatMenuModule, MatMenuTrigger } from '@angular/material/menu';
import { CurrentUser } from './current-user';

/** The header user switcher: every action is recorded under the chosen user. */
@Component({
  selector: 'app-user-switcher',
  imports: [MatButtonModule, MatMenuModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button mat-flat-button class="switcher" [matMenuTriggerFor]="menu" aria-label="Switch user" data-testid="user-switcher">
      {{ current.user()?.displayName ?? 'Choose user' }}
      @if (current.user(); as user) {
        <span class="role">· {{ user.role }}</span>
      }
      <svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true"><path fill="currentColor" d="M7 10l5 5 5-5z" /></svg>
    </button>
    <mat-menu #menu="matMenu">
      @if (current.users.hasValue()) {
        @for (user of current.users.value(); track user.id) {
          <button mat-menu-item (click)="current.switchTo(user.id)" [attr.aria-current]="user.id === current.id()">
            {{ user.displayName }} <span class="role">· {{ user.role }}</span>
          </button>
        }
      }
    </mat-menu>
  `,
  styles: `
    .switcher {
      --mat-button-filled-container-color: var(--mat-sys-surface);
      --mat-button-filled-label-text-color: var(--mat-sys-on-surface);
      white-space: nowrap;
    }
    .switcher svg {
      vertical-align: middle;
    }
    .role {
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-label-medium);
      margin-inline-start: 4px;
    }
    @media (max-width: 600px) {
      .switcher .role {
        display: none;
      }
    }
  `,
})
export class UserSwitcher {
  protected readonly current = inject(CurrentUser);
  private readonly trigger = viewChild.required(MatMenuTrigger);

  constructor() {
    effect(() => {
      if (this.current.switcherRequested()) this.trigger().openMenu();
    });
  }
}
