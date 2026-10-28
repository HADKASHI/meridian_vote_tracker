import { ChangeDetectionStrategy, Component } from '@angular/core';
import { PlaceholderPage } from '../../shared/ui/placeholder-page';

@Component({
  selector: 'app-positions-page',
  imports: [PlaceholderPage],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<app-placeholder-page heading="Positions" slice="slice 5"></app-placeholder-page>`,
})
export class PositionsPage {}
