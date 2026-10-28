import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/** Stands in for a screen until its slice builds it. */
@Component({
  selector: 'app-placeholder-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>{{ heading() }}</h1>
    <p class="note">This screen arrives with {{ slice() }}.</p>
    <ng-content />
  `,
  styles: `
    .note {
      opacity: 0.7;
    }
  `,
})
export class PlaceholderPage {
  readonly heading = input.required<string>();
  readonly slice = input.required<string>();
}
