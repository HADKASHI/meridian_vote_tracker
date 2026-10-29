import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/** Form-level errors, with the reference support uses to find what happened. */
@Component({
  selector: 'app-problem-banner',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (messages().length > 0) {
      <div class="notice" data-tone="error" role="alert">
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
    .notice {
      margin-block: 12px;
    }
    p {
      margin-block: 0 4px;
    }
    .reference {
      font-family: var(--app-mono);
      font-size: 0.85em;
      margin: 4px 0 0;
    }
  `,
})
export class ProblemBanner {
  readonly messages = input<string[]>([]);
  readonly traceId = input<string | undefined>();
}
