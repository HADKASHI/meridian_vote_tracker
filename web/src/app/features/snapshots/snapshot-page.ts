import { ChangeDetectionStrategy, Component, computed, input, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { JobDto, JobState } from '../../api/model';
import { getSnapshotResource } from '../../api/snapshots/snapshots.resource';
import { pollWhile } from '../../shared/http/poll';
import { JobStateBadge } from '../../shared/ui/job-state-badge';
import { LoadState } from '../../shared/ui/load-state';
import { PageHeader } from '../../shared/ui/page-header';
import { ScoreWithBasis } from '../../shared/ui/score';
import { BreakdownPanel } from './breakdown-panel';
import { ChangesPanel } from './changes-panel';
import { PublishPanel } from './publish-panel';
import { confidenceLabel, ConfidenceFilter, ConfidenceOrder, isLowEvidence, narrow, percent } from './evidence';
import { describeStoredSelection } from './selection';

/** SNP-03: one snapshot, followed while its job runs, then its ranking with each score's basis. */
@Component({
  selector: 'app-snapshot-page',
  imports: [
    DatePipe, MatButtonModule, MatTableModule, MatProgressBarModule, RouterLink, JobStateBadge, BreakdownPanel, ChangesPanel, PublishPanel, PageHeader,
    LoadState, ScoreWithBasis,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (snapshot.hasValue() && snapshot.value(); as s) {
      <app-page-header [heading]="s.name" [back]="{ label: 'Snapshots', link: '/snapshots' }">
        <p pageLead data-testid="snapshot-meta">
          {{ selectionLabel() }} · {{ s.voteCount }} {{ s.voteCount === 1 ? 'vote' : 'votes' }} · by {{ s.createdBy }} at
          {{ s.createdAt | date: 'yyyy-MM-dd HH:mm' }} · scored under {{ s.ruleSet.name }}
        </p>
      </app-page-header>
      @if (s.note) {
        <blockquote class="note">{{ s.note }}</blockquote>
      }
      <div class="surface-card job" data-testid="snapshot-job">
        <span>Job <app-job-state-badge [state]="s.job.state" /> {{ progress(s.job) }}</span>
        @if (s.job.state === 'queued' || s.job.state === 'running') {
          <mat-progress-bar [mode]="s.job.votesTotal && s.job.state === 'running' ? 'determinate' : 'indeterminate'"
            [value]="s.job.votesTotal ? (100 * s.job.votesDone) / s.job.votesTotal : 0" aria-label="Calculation progress" />
        }
      </div>

      @if (s.warnings.length > 0) {
        <section class="notice" data-tone="warning" data-testid="snapshot-warnings" aria-labelledby="snapshot-warnings">
          <h2 id="snapshot-warnings">Warnings (as they stood at calculation)</h2>
          <ul>
            @for (warning of s.warnings; track $index) {
              <li>{{ warning.message }}</li>
            }
          </ul>
        </section>
      }

      @if (s.publications.length > 0) {
        <ul class="publications" data-testid="snapshot-publications">
          @for (p of s.publications; track p.id) {
            <li>
              <span class="chip" [attr.data-tone]="p.supersededByPublicationId ? 'outline' : 'primary'">
                {{ p.supersededByPublicationId ? 'Was published' : 'Published' }} for {{ p.quarter }}
              </span>
              by {{ p.publishedBy }}
              @if (p.supersededByPublicationId) {
                · superseded by <a routerLink="/publications">publication #{{ p.supersededByPublicationId }}</a>
              }
            </li>
          }
        </ul>
      }

      @if (s.job.state === 'done') {
        <app-publish-panel [snapshotId]="s.id" (published)="snapshot.reload()" />
        <p>
          <a mat-stroked-button routerLink="/snapshots/compare" [queryParams]="{ a: s.id }" data-testid="compare-link">
            Compare with another snapshot
          </a>
        </p>
        <app-changes-panel [snapshotId]="s.id" />
        <div class="results">
          <section aria-labelledby="ranking-heading">
            <h2 id="ranking-heading">Ranking</h2>
            <div class="evidence-controls">
              <label>
                Confidence
                <select data-testid="filter-confidence" [value]="filter()" (change)="filter.set(choice($event))">
                  <option value="all">All states</option>
                  <option value="high">High</option>
                  <option value="medium">Medium</option>
                  <option value="low">Low (low evidence)</option>
                  <option value="none">No band</option>
                </select>
              </label>
              <label>
                Order
                <select data-testid="sort-confidence" [value]="order()" (change)="order.set(choice($event))">
                  <option value="rank">Rank</option>
                  <option value="confidence">Confidence, high first</option>
                </select>
              </label>
            </div>
            <p class="muted">
              Completeness is the share of votes the state was a member for with a yes or no; confidence never changes the rank.
            </p>
            @if (shown().length === 0) {
              <p class="muted" data-testid="ranking-empty">{{ emptyMessage() }}</p>
            }
            <div class="table-scroll">
              <table mat-table [dataSource]="shown()" data-testid="ranking">
                <ng-container matColumnDef="rank">
                  <th mat-header-cell *matHeaderCellDef>Rank</th>
                  <td mat-cell *matCellDef="let r" class="rank">{{ r.rank ?? '–' }}</td>
                </ng-container>
                <ng-container matColumnDef="state">
                  <th mat-header-cell *matHeaderCellDef>Member state</th>
                  <td mat-cell *matCellDef="let r" class="state">
                    <a [routerLink]="[]" [queryParams]="{ state: r.stateCode }"
                       [attr.aria-current]="r.stateCode === state() ? 'true' : null">{{ r.stateName }} ({{ r.stateCode }})</a>
                  </td>
                </ng-container>
                <ng-container matColumnDef="score">
                  <th mat-header-cell *matHeaderCellDef>Score and basis</th>
                  <td mat-cell *matCellDef="let r"><app-score [result]="r" [unit]="s.ruleSet.weightUnit" /></td>
                </ng-container>
                <ng-container matColumnDef="completeness">
                  <th mat-header-cell *matHeaderCellDef>Completeness</th>
                  <td mat-cell *matCellDef="let r" class="num">{{ percent(r.completeness) }}</td>
                </ng-container>
                <ng-container matColumnDef="abstention">
                  <th mat-header-cell *matHeaderCellDef>Abstention rate</th>
                  <td mat-cell *matCellDef="let r" class="num">{{ percent(r.abstentionRate) }}</td>
                </ng-container>
                <ng-container matColumnDef="missing">
                  <th mat-header-cell *matHeaderCellDef>Missing data</th>
                  <td mat-cell *matCellDef="let r" class="num">{{ percent(r.missingData) }}</td>
                </ng-container>
                <ng-container matColumnDef="confidence">
                  <th mat-header-cell *matHeaderCellDef>Confidence</th>
                  <td mat-cell *matCellDef="let r">
                    <span class="chip" [attr.data-tone]="r.confidence === 'low' ? 'error' : 'neutral'">{{ confidenceLabel(r.confidence) }}</span>&ngsp;
                    @if (isLowEvidence(r)) {
                      <span class="chip" data-tone="error" data-testid="low-evidence">low evidence</span>
                    }
                  </td>
                </ng-container>
                <tr mat-header-row *matHeaderRowDef="columns"></tr>
                <tr mat-row *matRowDef="let row; columns: columns" [class.selected]="row.stateCode === state()"></tr>
              </table>
            </div>
          </section>
          @if (state(); as code) {
            <section class="breakdown">
              <app-breakdown-panel [snapshotId]="s.id" [stateCode]="code" [weightUnit]="s.ruleSet.weightUnit" />
            </section>
          }
        </div>
      }
    } @else {
      <app-page-header heading="Snapshot" [back]="{ label: 'Snapshots', link: '/snapshots' }" />
      <p class="muted">Snapshot #{{ snapshotId() }}</p>
      @if (snapshot.error()) {
        <app-load-state kind="error" message="This snapshot could not be loaded." />
      } @else {
        <app-load-state kind="loading" message="Loading the snapshot" />
      }
    }
  `,
  styles: `
    .note {
      margin: 0 0 16px;
      padding-inline-start: 12px;
      border-inline-start: 3px solid var(--mat-sys-outline-variant);
      font-style: italic;
      overflow-wrap: anywhere;
    }
    .job span {
      display: inline-flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 6px;
    }
    .job mat-progress-bar {
      margin-block-start: 12px;
    }
    .notice h2 {
      font: var(--mat-sys-title-small);
      margin: 0 0 4px;
    }
    .publications {
      list-style: none;
      padding: 0;
      margin: 0 0 16px;
    }
    .publications li {
      margin-block-end: 4px;
    }
    .results {
      max-width: 960px;
    }
    .rank {
      font-variant-numeric: tabular-nums;
      width: 48px;
    }
    .state {
      min-width: 140px;
    }
    .selected {
      background: var(--mat-sys-secondary-container);
    }
    .num {
      font-variant-numeric: tabular-nums;
    }
    .evidence-controls {
      display: flex;
      flex-wrap: wrap;
      gap: 16px;
      margin-block-end: 8px;
    }
    .evidence-controls label {
      display: flex;
      flex-direction: column;
      gap: 4px;
      font: var(--mat-sys-label-large);
    }
    .state a[aria-current='true'] {
      font-weight: 600;
    }
  `,
})
export class SnapshotPage {
  readonly snapshotId = input.required<string>();
  /** The member state whose breakdown is open, from <code>?state=</code>. */
  readonly state = input<string>();

  protected readonly columns = ['rank', 'state', 'score', 'completeness', 'abstention', 'missing', 'confidence'];
  /** ADR 0007: narrowing and ordering by confidence only changes what is listed, never a rank. */
  protected readonly filter = signal<ConfidenceFilter>('all');
  protected readonly order = signal<ConfidenceOrder>('rank');
  protected readonly shown = computed(() => narrow(this.snapshot.hasValue() ? (this.snapshot.value()?.results ?? []) : [], this.filter(), this.order()));
  protected readonly emptyMessage = computed(() =>
    this.filter() === 'none'
      ? 'Every member state has a confidence band.'
      : `No member state has ${confidenceLabel(this.filter() as 'high' | 'medium' | 'low')} confidence.`,
  );
  protected readonly percent = percent;
  protected readonly confidenceLabel = confidenceLabel;
  protected readonly isLowEvidence = isLowEvidence;
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


  protected choice<T extends string>(event: Event): T {
    return (event.target as HTMLSelectElement).value as T;
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
