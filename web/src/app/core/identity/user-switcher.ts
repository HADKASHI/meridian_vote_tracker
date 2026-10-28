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
    <button mat-stroked-button [matMenuTriggerFor]="menu" aria-label="Switch user" data-testid="user-switcher">
      {{ current.user()?.displayName ?? 'Choose user' }}
      @if (current.user(); as user) {
        <span class="role">· {{ user.role }}</span>
      }
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
    .role {
      opacity: 0.7;
      margin-inline-start: 4px;
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
