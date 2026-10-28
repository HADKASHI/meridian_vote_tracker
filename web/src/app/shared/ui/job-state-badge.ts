import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { JobState } from '../../api/model';

/** A job's state in the glossary's words: queued, running, done or failed. */
@Component({
  selector: 'app-job-state-badge',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span class="badge" [attr.data-state]="state()">{{ state() }}</span>`,
  styles: `
    .badge {
      padding: 2px 8px;
      border-radius: 12px;
      background: var(--mat-sys-surface-container-high);
    }
    [data-state='failed'] {
      background: var(--mat-sys-error-container);
      color: var(--mat-sys-on-error-container);
    }
    [data-state='running'] {
      background: var(--mat-sys-primary-container);
      color: var(--mat-sys-on-primary-container);
    }
  `,
})
export class JobStateBadge {
  readonly state = input.required<JobState>();
}
