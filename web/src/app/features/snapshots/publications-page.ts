import { ChangeDetectionStrategy, Component } from '@angular/core';
import { PlaceholderPage } from '../../shared/ui/placeholder-page';

@Component({
  selector: 'app-publications-page',
  imports: [PlaceholderPage],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<app-placeholder-page heading="Publications" slice="slice 6"></app-placeholder-page>`,
})
export class PublicationsPage {}
