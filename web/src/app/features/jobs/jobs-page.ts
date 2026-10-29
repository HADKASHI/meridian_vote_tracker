import { ChangeDetectionStrategy, Component, computed, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { listJobsResource } from '../../api/jobs/jobs.resource';
import { JobDto, JobState } from '../../api/model';
import { pollWhile } from '../../shared/http/poll';
import { jobProgress } from '../../shared/ui/job-progress';
import { JobStateBadge } from '../../shared/ui/job-state-badge';
import { PageHeader } from '../../shared/ui/page-header';

/** Every job: kind, state, place in queue, progress and failure reason. */
@Component({
  selector: 'app-jobs-page',
  imports: [MatTableModule, MatProgressBarModule, DatePipe, RouterLink, JobStateBadge, PageHeader],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header heading="Jobs">
      <p pageLead>Imports and snapshot calculations run here in the background, oldest first.</p>
    </app-page-header>
    @if (jobs.hasValue() && jobs.value(); as page) {
      <div class="table-scroll">
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
          <td mat-cell *matCellDef="let job" class="progress">
            {{ progress(job) }}
            @if (job.state === 'running') {
              <mat-progress-bar [mode]="job.votesTotal ? 'determinate' : 'indeterminate'"
                [value]="job.votesTotal ? (100 * job.votesDone) / job.votesTotal : 0" aria-label="Job progress" />
            }
          </td>
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
      </div>
      @if (page.items.length === 0) {
        <p class="surface-card muted">No jobs yet. Uploading a file or calculating a snapshot starts one.</p>
      }
    } @else if (jobs.error()) {
      <p class="surface-card" role="alert">The jobs could not be loaded.</p>
    } @else {
      <mat-progress-bar mode="indeterminate" aria-label="Loading jobs" />
    }
  `,
  styles: `
    .progress {
      min-width: 160px;
    }
    .progress mat-progress-bar {
      margin-block-start: 4px;
    }
    td {
      white-space: nowrap;
    }
    td.progress {
      white-space: normal;
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
