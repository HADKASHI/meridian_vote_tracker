import { ChangeDetectionStrategy, Component, computed, input, signal } from '@angular/core';

/**
 * A list the data can make long (69 missing member states): up to `inlineMax` items show
 * inline; more show as their count, which expands to the items.
 */
@Component({
  selector: 'app-expandable-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (items().length <= inlineMax()) {
      {{ items().join(', ') }}
    } @else {
      <button type="button" class="toggle" [attr.aria-expanded]="open()" (click)="open.set(!open())">
        {{ label() }}
      </button>
      @if (open()) {
        <span class="items">{{ items().join(', ') }}</span>
      }
    }
  `,
  styles: `
    :host {
      overflow-wrap: anywhere;
    }
    .toggle {
      font: inherit;
      color: var(--mat-sys-primary);
      background: none;
      border: none;
      padding: 0;
      cursor: pointer;
      text-decoration: underline;
    }
    .toggle::after {
      content: ' ▸';
      text-decoration: none;
      display: inline-block;
      margin-inline-start: 2px;
    }
    .toggle[aria-expanded='true']::after {
      content: ' ▾';
    }
    .items {
      display: block;
      margin-block-start: 4px;
    }
  `,
})
export class ExpandableList {
  readonly items = input.required<readonly string[]>();
  /** What the items are, plural: "missing states". */
  readonly noun = input.required<string>();
  readonly inlineMax = input(8);
  protected readonly open = signal(false);
  protected readonly label = computed(() => `${this.items().length} ${this.noun()}`);
}
