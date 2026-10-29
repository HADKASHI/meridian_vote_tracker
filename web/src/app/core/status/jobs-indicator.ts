import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { RouterLink } from '@angular/router';
import { ShellStatus } from './shell-status';

/** "2 jobs running · 1 queued", linking to the Jobs list. */
@Component({
  selector: 'app-jobs-indicator',
  imports: [RouterLink, MatProgressSpinnerModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <a routerLink="/jobs" data-testid="jobs-indicator" [class.busy]="busy()">
      @if (busy()) {
        <mat-spinner diameter="14" strokeWidth="2" aria-hidden="true" />
      }
      <span class="label">{{ label() }}</span>
    </a>
  `,
  styles: `
    a {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      padding: 4px 12px;
      border-radius: 16px;
      color: inherit;
      text-decoration: none;
      font: var(--mat-sys-label-large);
      white-space: nowrap;
      background: color-mix(in srgb, var(--mat-sys-on-primary) 14%, transparent);
    }
    a:hover {
      background: color-mix(in srgb, var(--mat-sys-on-primary) 22%, transparent);
    }
    mat-spinner {
      --mat-progress-spinner-active-indicator-color: var(--mat-sys-on-primary);
    }
    /* On a phone the idle chip gives way; Jobs stays in the navigation menu. */
    @media (max-width: 600px) {
      a:not(.busy) {
        display: none;
      }
    }
  `,
})
export class JobsIndicator {
  private readonly shell = inject(ShellStatus);

  protected readonly busy = computed(() => this.shell.running().length > 0);
  protected readonly label = computed(() => jobsLabel(this.shell.running().length, this.shell.queued().length));
}

export function jobsLabel(running: number, queued: number): string {
  if (running === 0 && queued === 0) return 'No jobs running';
  const parts = [`${running} ${running === 1 ? 'job' : 'jobs'} running`];
  if (queued > 0) parts.push(`${queued} queued`);
  return parts.join(' · ');
}
