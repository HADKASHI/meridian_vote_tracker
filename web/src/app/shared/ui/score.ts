import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/** What a member state's score rests on. */
export interface ScoreFacts {
  score: number | null;
  countableVotes: number;
  matchedWeight: number;
  countedWeight: number;
}

/** A score to 1 decimal, or n/a when nothing is countable. */
export function formatScore(score: number | null): string {
  return score === null ? 'n/a' : score.toFixed(1);
}

/** "10 countable votes · 20 of 32 weight matched", or "nothing countable". */
export function scoreBasis(facts: Omit<ScoreFacts, 'score'>): string {
  if (facts.countableVotes === 0) return 'nothing countable';
  const votes = `${facts.countableVotes} countable ${facts.countableVotes === 1 ? 'vote' : 'votes'}`;
  return `${votes} · ${facts.matchedWeight} of ${facts.countedWeight} weight matched`;
}

/** A score to 1 decimal (n/a when nothing is countable), always beside its basis. */
@Component({
  selector: 'app-score',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span class="score">{{ score() }}</span>&ngsp;<span class="basis">{{ basis() }}</span>`,
  styles: `
    :host {
      display: inline-flex;
      flex-wrap: wrap;
      align-items: baseline;
      gap: 0 8px;
    }
    .score {
      font: var(--mat-sys-title-medium);
      font-variant-numeric: tabular-nums;
      min-width: 3.5ch;
    }
    .basis {
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
    }
  `,
})
export class ScoreWithBasis {
  readonly result = input.required<ScoreFacts>();
  protected readonly score = computed(() => formatScore(this.result().score));
  protected readonly basis = computed(() => scoreBasis(this.result()));
}
