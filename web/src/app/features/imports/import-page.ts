import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { PlaceholderPage } from '../../shared/ui/placeholder-page';

@Component({
  selector: 'app-import-page',
  imports: [PlaceholderPage],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<app-placeholder-page heading="Import report" slice="slice 1"><p>Import #{{ importId() }}</p></app-placeholder-page>`,
})
export class ImportPage {
  readonly importId = input.required<string>();
}
