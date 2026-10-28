import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { PlaceholderPage } from '../../shared/ui/placeholder-page';

@Component({
  selector: 'app-snapshot-page',
  imports: [PlaceholderPage],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<app-placeholder-page heading="Snapshot" slice="slice 6"><p>Snapshot #{{ snapshotId() }}</p></app-placeholder-page>`,
})
export class SnapshotPage {
  readonly snapshotId = input.required<string>();
}
