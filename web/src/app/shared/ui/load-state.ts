import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatProgressBarModule } from '@angular/material/progress-bar';

/** One look for a list or page that is loading, has nothing to show, or failed to load. */
@Component({
  selector: 'app-load-state',
  imports: [MatProgressBarModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @switch (kind()) {
      @case ('loading') {
        <mat-progress-bar mode="indeterminate" [attr.aria-label]="message()" />
      }
      @case ('empty') {
        <p class="state">{{ message() }}</p>
      }
      @case ('error') {
        <p class="state error" role="alert">{{ message() }}</p>
      }
    }
  `,
  styles: `
    :host {
      display: block;
      margin-block: 8px 16px;
    }
    .state {
      margin: 0;
      padding: 24px 16px;
      text-align: center;
      border: 1px dashed var(--mat-sys-outline-variant);
      border-radius: 12px;
      color: var(--mat-sys-on-surface-variant);
      background: var(--mat-sys-surface);
    }
    .error {
      border-style: solid;
      border-color: var(--mat-sys-error);
      color: var(--mat-sys-on-error-container);
      background: var(--mat-sys-error-container);
    }
  `,
})
export class LoadState {
  readonly kind = input.required<'loading' | 'empty' | 'error'>();
  readonly message = input.required<string>();
}
