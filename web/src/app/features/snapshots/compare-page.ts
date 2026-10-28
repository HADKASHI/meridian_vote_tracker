import { ChangeDetectionStrategy, Component } from '@angular/core';
import { PlaceholderPage } from '../../shared/ui/placeholder-page';

@Component({
  selector: 'app-compare-page',
  imports: [PlaceholderPage],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<app-placeholder-page heading="Compare snapshots" slice="slice 7"></app-placeholder-page>`,
})
export class ComparePage {}
