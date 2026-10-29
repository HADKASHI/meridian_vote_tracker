import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { Router, RouterLink } from '@angular/router';
import { MatTableModule } from '@angular/material/table';
import { HttpErrorResponse } from '@angular/common/http';
import { JobState, ScoreBasisDto } from '../../api/model';
import { compareSnapshotsResource, listSnapshotsResource } from '../../api/snapshots/snapshots.resource';
import { LoadState } from '../../shared/ui/load-state';
import { PageHeader } from '../../shared/ui/page-header';
import { formatScore, scoreBasis } from '../../shared/ui/score';

/** "3 · 50.0", or n/a when the state has no rank in that snapshot. */
export function rankAndScore(rank: number | null, score: number | null): string {
  return rank === null ? 'n/a' : `${rank} · ${formatScore(score)}`;
}

/** What the score rests on, or nothing when the snapshot has no result for the state. */
export function basisOf(basis: ScoreBasisDto | null): string {
  return basis ? scoreBasis(basis) : '';
}

/** The score change with its sign, or no movement when either snapshot has no score. */
export function scoreMovement(change: number | null): string {
  if (change === null) return 'no movement';
  const text = Math.abs(change).toFixed(1);
  return change > 0.05 ? `+${text}` : change < -0.05 ? `−${text}` : '0.0';
}

/** Rank change in words as well as arrows; a positive change means the state moved up. */
export function rankMovement(change: number | null): string {
  if (change === null) return 'no movement';
  return change > 0 ? `▲ up ${change}` : change < 0 ? `▼ down ${-change}` : '= unchanged';
}

/** Every state's rank and score in two snapshots and how far it moved, read from what they stored. */
@Component({
  selector: 'app-snapshot-movement',
  imports: [MatTableModule, LoadState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (comparison.hasValue() && comparison.value(); as c) {
      <div class="table-scroll">
        <table mat-table [dataSource]="c.states" data-testid="movement">
          <ng-container matColumnDef="state">
            <th mat-header-cell *matHeaderCellDef>Member state</th>
            <td mat-cell *matCellDef="let s">{{ s.stateName }} ({{ s.stateCode }})</td>
          </ng-container>
          <ng-container matColumnDef="a">
            <th mat-header-cell *matHeaderCellDef>Rank · score in {{ c.a.name }}</th>
            <td mat-cell *matCellDef="let s" class="num">
              {{ rankAndScore(s.rankA, s.scoreA) }} <span class="basis">{{ basisOf(s.basisA) }}</span>
            </td>
          </ng-container>
          <ng-container matColumnDef="b">
            <th mat-header-cell *matHeaderCellDef>Rank · score in {{ c.b.name }}</th>
            <td mat-cell *matCellDef="let s" class="num">
              {{ rankAndScore(s.rankB, s.scoreB) }} <span class="basis">{{ basisOf(s.basisB) }}</span>
            </td>
          </ng-container>
          <ng-container matColumnDef="score">
            <th mat-header-cell *matHeaderCellDef>Score change</th>
            <td mat-cell *matCellDef="let s" class="num">{{ scoreMovement(s.scoreChange) }}</td>
          </ng-container>
          <ng-container matColumnDef="rank">
            <th mat-header-cell *matHeaderCellDef>Rank change</th>
            <td mat-cell *matCellDef="let s">{{ rankMovement(s.rankChange) }}</td>
          </ng-container>
          <tr mat-header-row *matHeaderRowDef="columns"></tr>
          <tr mat-row *matRowDef="let row; columns: columns"></tr>
        </table>
      </div>
    } @else if (comparison.error()) {
      <app-load-state kind="error" [message]="failure()" />
    } @else {
      <app-load-state kind="loading" message="Loading the comparison" />
    }
  `,
  styles: `
    .num {
      font-variant-numeric: tabular-nums;
    }
    .basis {
      display: block;
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
    }
  `,
})
export class SnapshotMovement {
  readonly a = input.required<number>();
  readonly b = input.required<number>();

  protected readonly columns = ['state', 'a', 'b', 'score', 'rank'];
  protected readonly comparison = compareSnapshotsResource(computed(() => ({ a: this.a(), b: this.b() })));
  protected readonly failure = computed(() => {
    const error = this.comparison.error();
    return error instanceof HttpErrorResponse && error.status === 404
      ? 'One of these snapshots does not exist.'
      : 'These snapshots could not be compared.';
  });
  protected readonly rankAndScore = rankAndScore;
  protected readonly basisOf = basisOf;
  protected readonly scoreMovement = scoreMovement;
  protected readonly rankMovement = rankMovement;
}

/** Extra (story 102): choose two calculated snapshots, held in <code>?a=</code> and <code>?b=</code>, and compare them. */
@Component({
  selector: 'app-compare-page',
  imports: [MatFormFieldModule, MatInputModule, RouterLink, PageHeader, LoadState, SnapshotMovement],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header heading="Compare snapshots" [back]="{ label: 'Snapshots', link: '/snapshots' }">
      <p pageLead>See how each member state's rank and score moved between two saved snapshots. Nothing is recalculated.</p>
    </app-page-header>
    @if (snapshots.error()) {
      <app-load-state kind="error" message="The snapshots could not be loaded." />
    } @else if (!snapshots.hasValue()) {
      <app-load-state kind="loading" message="Loading snapshots" />
    } @else {
      <div class="pickers">
        @for (side of sides; track side.key) {
          <mat-form-field>
            <mat-label>Snapshot {{ side.key.toUpperCase() }}</mat-label>
            <select matNativeControl [attr.data-testid]="'pick-' + side.key" [value]="side.value()" (change)="pick(side.key, $event)">
              <option value="">Choose a snapshot</option>
              @for (s of calculated(); track s.id) {
                <option [value]="s.id" [selected]="'' + s.id === side.value()">{{ s.name }}</option>
              }
            </select>
          </mat-form-field>
        }
      </div>
      @if (a() && b()) {
        @if (a() === b()) {
          <app-load-state kind="empty" message="Choose two different snapshots to compare." />
        } @else {
          <app-snapshot-movement [a]="+a()!" [b]="+b()!" />
        }
      } @else {
        <app-load-state kind="empty" message="Choose snapshot A and snapshot B to compare them." />
      }
      <p><a routerLink="/snapshots">All snapshots</a></p>
    }
  `,
  styles: `
    .pickers {
      display: flex;
      flex-wrap: wrap;
      gap: 16px;
    }
    mat-form-field {
      min-width: 240px;
    }
  `,
})
export class ComparePage {
  /** From <code>?a=</code> and <code>?b=</code>. */
  readonly a = input<string>();
  readonly b = input<string>();

  private readonly router = inject(Router);
  protected readonly snapshots = listSnapshotsResource(computed(() => ({ page: 1, pageSize: 200 })));
  protected readonly calculated = computed(() =>
    (this.snapshots.hasValue() ? this.snapshots.value()?.items ?? [] : []).filter((s) => s.job.state === JobState.done),
  );
  protected readonly sides = [
    { key: 'a' as const, value: computed(() => this.a() ?? '') },
    { key: 'b' as const, value: computed(() => this.b() ?? '') },
  ];

  protected pick(side: 'a' | 'b', event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    void this.router.navigate([], { queryParams: { [side]: value || null }, queryParamsHandling: 'merge' });
  }
}

