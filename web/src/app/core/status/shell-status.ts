import { computed, inject, Injectable, Injector, runInInjectionContext } from '@angular/core';
import { getStatusResource } from '../../api/jobs/jobs.resource';
import { JobDto } from '../../api/model';
import { pollWhile } from '../../shared/http/poll';

export const ACTIVE_POLL_MS = 1000;
export const IDLE_POLL_MS = 5000;

/**
 * One poll of GET /api/status for the whole shell: the jobs indicator and the Review
 * badge. Every second while a job is queued or running, otherwise every 5 seconds;
 * paused while the tab is hidden.
 */
@Injectable({ providedIn: 'root' })
export class ShellStatus {
  readonly status = getStatusResource();

  readonly running = computed(() => (this.status.hasValue() ? this.status.value()?.running ?? [] : []));
  readonly queued = computed(() => (this.status.hasValue() ? this.status.value()?.queued ?? [] : []));
  readonly recentlyFinished = computed<JobDto[]>(() =>
    this.status.hasValue() ? this.status.value()?.recentlyFinished ?? [] : [],
  );
  readonly pendingConflicts = computed(() => (this.status.hasValue() ? this.status.value()?.pendingConflicts ?? 0 : 0));
  readonly active = computed(() => this.running().length + this.queued().length > 0);

  constructor() {
    runInInjectionContext(inject(Injector), () =>
      pollWhile(this.status, () => (this.active() ? ACTIVE_POLL_MS : IDLE_POLL_MS), () => true),
    );
  }
}
