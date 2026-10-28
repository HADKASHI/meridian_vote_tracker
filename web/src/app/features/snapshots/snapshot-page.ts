import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { JobDto, JobState, SnapshotResultDto } from '../../api/model';
import { getSnapshotResource } from '../../api/snapshots/snapshots.resource';
import { pollWhile } from '../../shared/http/poll';
import { JobStateBadge } from '../../shared/ui/job-state-badge';
import { BreakdownPanel } from './breakdown-panel';
import { describeStoredSelection, formatScore } from './selection';

/** SNP-03: one snapshot, followed while its job runs, then its ranking with each score's basis. */
@Component({
  selector: 'app-snapshot-page',
  imports: [DatePipe, MatTableModule, RouterLink, JobStateBadge, BreakdownPanel],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (snapshot.hasValue() && snapshot.value(); as s) {
      <h1>{{ s.name }}</h1>
      <p class="meta" data-testid="snapshot-meta">
        {{ selectionLabel() }} · {{ s.voteCount }} {{ s.voteCount === 1 ? 'vote' : 'votes' }} · by {{ s.createdBy }} at
        {{ s.createdAt | date: 'yyyy-MM-dd HH:mm' }} · scoring rules {{ s.scoringRulesVersion }}
      </p>
      @if (s.note) {
        <p class="note">{{ s.note }}</p>
      }
      <p data-testid="snapshot-job"><app-job-state-badge [state]="s.job.state" /> {{ progress(s.job) }}</p>

      @if (s.warnings.length > 0) {
        <section class="warnings" data-testid="snapshot-warnings">
          <h2>Warnings (as they stood at calculation)</h2>
          <ul>
            @for (warning of s.warnings; track $index) {
              <li>{{ warning.message }}</li>
            }
          </ul>
        </section>
      }

      @if (s.job.state === 'done') {
        <h2>Ranking</h2>
        <table mat-table [dataSource]="s.results" data-testid="ranking">
          <ng-container matColumnDef="rank">
            <th mat-header-cell *matHeaderCellDef>Rank</th>
            <td mat-cell *matCellDef="let r">{{ r.rank ?? '–' }}</td>
          </ng-container>
          <ng-container matColumnDef="state">
            <th mat-header-cell *matHeaderCellDef>Member state</th>
            <td mat-cell *matCellDef="let r">
              <a [routerLink]="[]" [queryParams]="{ state: r.stateCode }">{{ r.stateName }} ({{ r.stateCode }})</a>
            </td>
          </ng-container>
          <ng-container matColumnDef="score">
            <th mat-header-cell *matHeaderCellDef>Score</th>
            <td mat-cell *matCellDef="let r">{{ score(r) }}</td>
          </ng-container>
          <ng-container matColumnDef="basis">
            <th mat-header-cell *matHeaderCellDef>Basis</th>
            <td mat-cell *matCellDef="let r">{{ basis(r) }}</td>
          </ng-container>
          <tr mat-header-row *matHeaderRowDef="columns"></tr>
          <tr mat-row *matRowDef="let row; columns: columns" [class.selected]="row.stateCode === state()"></tr>
        </table>
        @if (state(); as code) {
          <app-breakdown-panel [snapshotId]="s.id" [stateCode]="code" />
        }
      }
    } @else {
      <h1>Snapshot</h1>
      <p>Snapshot #{{ snapshotId() }}</p>
      @if (snapshot.error()) {
        <p role="alert">This snapshot could not be loaded.</p>
      }
    }
  `,
  styles: `
    .meta {
      opacity: 0.8;
    }
    .warnings {
      border-inline-start: 4px solid var(--mat-sys-tertiary);
      background: var(--mat-sys-tertiary-container);
      color: var(--mat-sys-on-tertiary-container);
      border-radius: 8px;
      padding: 8px 16px;
    }
    .selected {
      background: var(--mat-sys-surface-container-high);
    }
    .warnings h2 {
      font-size: 1em;
      margin-block: 4px;
    }
  `,
})
export class SnapshotPage {
  readonly snapshotId = input.required<string>();
  /** The member state whose breakdown is open, from <code>?state=</code>. */
  readonly state = input<string>();

  protected readonly columns = ['rank', 'state', 'score', 'basis'];
  protected readonly snapshot = getSnapshotResource(computed(() => Number(this.snapshotId())));
  protected readonly selectionLabel = computed(() =>
    this.snapshot.hasValue() && this.snapshot.value() ? describeStoredSelection(this.snapshot.value()!.selection) : '',
  );
  private readonly active = computed(() => {
    const state = this.snapshot.hasValue() ? this.snapshot.value()?.job.state : undefined;
    return state === JobState.queued || state === JobState.running;
  });

  constructor() {
    pollWhile(this.snapshot, 1000, this.active);
  }

  protected score(result: SnapshotResultDto): string {
    return formatScore(result.score);
  }

  protected basis(result: SnapshotResultDto): string {
    if (result.countableVotes === 0) return 'nothing countable';
    const votes = `${result.countableVotes} countable ${result.countableVotes === 1 ? 'vote' : 'votes'}`;
    return `${votes}, ${result.matchedWeight} of ${result.countedWeight} weight matched`;
  }

  protected progress(job: JobDto): string {
    switch (job.state) {
      case JobState.queued:
        return job.placeInQueue === 1 ? 'Next in queue' : `${(job.placeInQueue ?? 1) - 1} ahead in queue`;
      case JobState.running:
        return job.votesTotal ? `Calculating over ${job.votesTotal.toLocaleString('en')} votes` : 'Calculating';
      case JobState.done:
        return 'Calculated';
      case JobState.failed:
        return `Failed: ${job.lastError ?? 'no reason recorded'}`;
    }
  }
}
