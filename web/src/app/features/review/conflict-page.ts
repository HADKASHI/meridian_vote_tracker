import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { PlaceholderPage } from '../../shared/ui/placeholder-page';

@Component({
  selector: 'app-conflict-page',
  imports: [PlaceholderPage],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<app-placeholder-page heading="Conflict" slice="slice 3"><p>Conflict #{{ conflictId() }}</p></app-placeholder-page>`,
})
export class ConflictPage {
  readonly conflictId = input.required<string>();
}
