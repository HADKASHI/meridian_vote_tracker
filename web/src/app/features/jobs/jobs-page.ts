import { ChangeDetectionStrategy, Component, computed, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { listJobsResource } from '../../api/jobs/jobs.resource';
import { JobDto, JobState } from '../../api/model';
import { pollWhile } from '../../shared/http/poll';
import { JobStateBadge } from '../../shared/ui/job-state-badge';

/** Every job: kind, state, place in queue, progress and failure reason. */
@Component({
  selector: 'app-jobs-page',
  imports: [MatTableModule, DatePipe, RouterLink, JobStateBadge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>Jobs</h1>
    @if (jobs.hasValue() && jobs.value(); as page) {
      <table mat-table [dataSource]="page.items" data-testid="jobs-table">
        <ng-container matColumnDef="job">
          <th mat-header-cell *matHeaderCellDef>Job</th>
          <td mat-cell *matCellDef="let job">
            @if (job.kind === 'import') {
              <a [routerLink]="['/imports', job.importId]">Import #{{ job.importId }}</a>
            } @else {
              <a [routerLink]="['/snapshots', job.snapshotId]">Snapshot #{{ job.snapshotId }}</a>
            }
          </td>
        </ng-container>
        <ng-container matColumnDef="state">
          <th mat-header-cell *matHeaderCellDef>State</th>
          <td mat-cell *matCellDef="let job"><app-job-state-badge [state]="job.state" /></td>
        </ng-container>
        <ng-container matColumnDef="progress">
          <th mat-header-cell *matHeaderCellDef>Progress</th>
          <td mat-cell *matCellDef="let job">{{ progress(job) }}</td>
        </ng-container>
        <ng-container matColumnDef="startedBy">
          <th mat-header-cell *matHeaderCellDef>By</th>
          <td mat-cell *matCellDef="let job">{{ job.startedBy }}</td>
        </ng-container>
        <ng-container matColumnDef="createdAt">
          <th mat-header-cell *matHeaderCellDef>Queued at</th>
          <td mat-cell *matCellDef="let job">{{ job.createdAt | date: 'yyyy-MM-dd HH:mm:ss' }}</td>
        </ng-container>
        <tr mat-header-row *matHeaderRowDef="columns"></tr>
        <tr mat-row *matRowDef="let row; columns: columns"></tr>
      </table>
      @if (page.items.length === 0) {
        <p>No jobs yet.</p>
      }
    }
  `,
})
export class JobsPage {
  protected readonly columns = ['job', 'state', 'progress', 'startedBy', 'createdAt'];
  protected readonly params = signal({ page: 1, pageSize: 100 });
  protected readonly jobs = listJobsResource(this.params);
  private readonly active = computed(() =>
    (this.jobs.hasValue() ? this.jobs.value()?.items ?? [] : []).some(
      (j) => j.state === JobState.queued || j.state === JobState.running,
    ),
  );

  constructor() {
    pollWhile(this.jobs, 1000, this.active);
  }

  protected progress(job: JobDto): string {
    return jobProgress(job);
  }
}

/** The words a job's progress is shown in, everywhere. */
export function jobProgress(job: JobDto): string {
  switch (job.state) {
    case JobState.queued:
      return job.placeInQueue === 1 ? 'Next in queue' : `${(job.placeInQueue ?? 1) - 1} ahead in queue`;
    case JobState.running:
      return job.votesTotal
        ? `${job.votesDone.toLocaleString('en')} of ${job.votesTotal.toLocaleString('en')} votes`
        : `${job.rowsRead.toLocaleString('en')} rows read`;
    case JobState.done:
      return job.votesTotal ? `${job.votesTotal.toLocaleString('en')} votes` : 'Done';
    case JobState.failed:
      return job.votesTotal
        ? `Failed after ${job.votesDone.toLocaleString('en')} of ${job.votesTotal.toLocaleString('en')} votes: ${job.lastError ?? ''}`
        : `Failed: ${job.lastError ?? ''}`;
  }
}
