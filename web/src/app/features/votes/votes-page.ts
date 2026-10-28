import { ChangeDetectionStrategy, Component } from '@angular/core';
import { PlaceholderPage } from '../../shared/ui/placeholder-page';

@Component({
  selector: 'app-votes-page',
  imports: [PlaceholderPage],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<app-placeholder-page heading="Votes" slice="slice 4"></app-placeholder-page>`,
})
export class VotesPage {}
