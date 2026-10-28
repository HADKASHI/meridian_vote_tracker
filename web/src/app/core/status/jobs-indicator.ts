import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ShellStatus } from './shell-status';

/** "2 jobs running · 1 queued", linking to the Jobs list. */
@Component({
  selector: 'app-jobs-indicator',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<a routerLink="/jobs" data-testid="jobs-indicator">{{ label() }}</a>`,
  styles: `
    a {
      color: inherit;
    }
  `,
})
export class JobsIndicator {
  private readonly shell = inject(ShellStatus);

  protected readonly label = computed(() => jobsLabel(this.shell.running().length, this.shell.queued().length));
}

export function jobsLabel(running: number, queued: number): string {
  if (running === 0 && queued === 0) return 'No jobs running';
  const parts = [`${running} ${running === 1 ? 'job' : 'jobs'} running`];
  if (queued > 0) parts.push(`${queued} queued`);
  return parts.join(' · ');
}
