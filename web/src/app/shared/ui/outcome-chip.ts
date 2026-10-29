import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { Outcome } from '../../api/model';

/** Each outcome's tone; the word itself always shows, so the tone only helps scanning. */
const TONES: Record<Outcome, string> = {
  new: 'primary',
  unchanged: 'neutral',
  duplicate: 'secondary',
  invalid: 'error',
  contradiction: 'tertiary',
  conflict: 'strong',
};

/** A row's outcome in the glossary's word, optionally with how many rows had it. */
@Component({
  selector: 'app-outcome-chip',
  imports: [DecimalPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span class="chip" [attr.data-tone]="tone()">{{ outcome() }}@if (count() !== undefined) {&ngsp;<strong>{{ count() | number }}</strong>
    }</span>`,
})
export class OutcomeChip {
  readonly outcome = input.required<Outcome>();
  readonly count = input<number | undefined>();
  protected readonly tone = computed(() => TONES[this.outcome()]);
}
