import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/** A change from one value to another: the current value, then the incoming one in bold. */
@Component({
  selector: 'app-from-to',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span class="from">{{ from() }}</span>&ngsp;<span class="arrow" role="img" aria-label="to">→</span>&ngsp;<strong class="to">{{ to() }}</strong>`,
  styles: `
    :host {
      overflow-wrap: anywhere;
    }
    .from {
      color: var(--mat-sys-on-surface-variant);
    }
    .arrow {
      color: var(--mat-sys-on-surface-variant);
    }
  `,
})
export class FromTo {
  readonly from = input.required<string>();
  readonly to = input.required<string>();
}
