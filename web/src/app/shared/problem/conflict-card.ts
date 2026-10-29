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
      background: var(--mat-sys-surface-container);
      border: 1px solid var(--mat-sys-outline-variant);
      border-inline-start: 4px solid var(--mat-sys-tertiary);
      border-radius: 12px;
      padding: 16px;
      margin-block: 12px;
      overflow-wrap: anywhere;
    }
    h3 {
      font: var(--mat-sys-title-medium);
      margin-block: 0 8px;
    }
    .actions {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 8px;
      margin-block-start: 12px;
    }
    .actions:empty {
      display: none;
    }
    .reference {
      font-family: var(--app-mono);
      color: var(--mat-sys-on-surface-variant);
      font-size: 0.85em;
      margin-block: 8px 0;
    }
  `,
})
export class ConflictCard {
  readonly heading = input.required<string>();
  readonly traceId = input<string | undefined>();
}
