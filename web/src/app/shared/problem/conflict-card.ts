import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * The 409 card: shown in place of the action that lost (a decision, a position save, a
 * publication), with the current state someone else just made. Never a toast, never an
 * automatic retry. The body says what is current; the actions slot offers what to do next.
 */
@Component({
  selector: 'app-conflict-card',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="card" role="status" aria-live="polite">
      <h3>{{ heading() }}</h3>
      <ng-content />
      <div class="actions"><ng-content select="[cardActions]" /></div>
      @if (traceId(); as id) {
        <p class="reference">Reference: {{ id }}</p>
      }
    </section>
  `,
  styles: `
    .card {
      border: 1px solid var(--mat-sys-outline);
      border-inline-start: 4px solid var(--mat-sys-tertiary);
      border-radius: 8px;
      padding: 12px 16px;
    }
    h3 {
      margin-block: 0 8px;
    }
    .actions {
      display: flex;
      gap: 8px;
      margin-block-start: 8px;
    }
    .reference {
      font-family: monospace;
      opacity: 0.7;
      font-size: 0.85em;
    }
  `,
})
export class ConflictCard {
  readonly heading = input.required<string>();
  readonly traceId = input<string | undefined>();
}
