import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/** Form-level errors, with the reference support uses to find what happened. */
@Component({
  selector: 'app-problem-banner',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (messages().length > 0) {
      <div class="banner" role="alert">
        @for (message of messages(); track $index) {
          <p>{{ message }}</p>
        }
        @if (traceId(); as id) {
          <p class="reference">Reference: {{ id }}</p>
        }
      </div>
    }
  `,
  styles: `
    .banner {
      border-inline-start: 4px solid var(--mat-sys-error);
      background: var(--mat-sys-error-container);
      color: var(--mat-sys-on-error-container);
      padding: 8px 16px;
      margin-block: 8px;
    }
    .reference {
      font-family: monospace;
      opacity: 0.8;
    }
  `,
})
export class ProblemBanner {
  readonly messages = input<string[]>([]);
  readonly traceId = input<string | undefined>();
}
