import { effect, inject, Injectable } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Router } from '@angular/router';
import { JobDto, JobState } from '../../api/model';
import { CurrentUser } from '../identity/current-user';
import { jobTarget } from './job-target';
import { ShellStatus } from './shell-status';

/** Announces once, with a link, when a job the current user started is done or has failed. */
@Injectable({ providedIn: 'root' })
export class JobNotices {
  private readonly announced = new Set<number>();
  private readonly snackBar = inject(MatSnackBar);
  private readonly router = inject(Router);

  constructor() {
    const shell = inject(ShellStatus);
    const user = inject(CurrentUser);
    let first = true;
    effect(() => {
      const finished = shell.recentlyFinished();
      if (!shell.status.hasValue()) return;
      // Jobs already finished when the page loaded were announced elsewhere, or not at all.
      if (first) {
        finished.forEach((job) => this.announced.add(job.id));
        first = false;
        return;
      }
      for (const job of finished) {
        if (this.announced.has(job.id) || job.startedBy !== user.id()) continue;
        this.announced.add(job.id);
        this.announce(job);
      }
    });
  }

  private announce(job: JobDto): void {
    const { label: what, link } = jobTarget(job);
    const message = job.state === JobState.done ? `${what} is done.` : `${what} failed: ${job.lastError ?? 'see the job'}`;
    this.snackBar
      .open(message, 'Open', { duration: 8000 })
      .onAction()
      .subscribe(() => this.router.navigate(link));
  }
}
