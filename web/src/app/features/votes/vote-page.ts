import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { PlaceholderPage } from '../../shared/ui/placeholder-page';

@Component({
  selector: 'app-vote-page',
  imports: [PlaceholderPage],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<app-placeholder-page heading="Vote" slice="slice 4"><p>Vote #{{ voteId() }}{{ tab() ? ' · ' + tab() : '' }}</p></app-placeholder-page>`,
})
export class VotePage {
  readonly voteId = input.required<string>();
  readonly tab = input<string>();
}
