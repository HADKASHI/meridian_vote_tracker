import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { JobState } from '../../api/model';

const TONES: Record<JobState, string> = {
  queued: 'outline',
  running: 'primary',
  done: 'secondary',
  failed: 'error',
};

/** A job's state in the glossary's words: queued, running, done or failed. */
@Component({
  selector: 'app-job-state-badge',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span class="chip" [attr.data-state]="state()" [attr.data-tone]="tone()">{{ state() }}</span>`,
})
export class JobStateBadge {
  readonly state = input.required<JobState>();
  protected readonly tone = computed(() => TONES[this.state()]);
}
