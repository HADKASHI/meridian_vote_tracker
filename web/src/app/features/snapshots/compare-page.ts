import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { Router, RouterLink } from '@angular/router';
import { MatTableModule } from '@angular/material/table';
import { HttpErrorResponse } from '@angular/common/http';
import { JobState, MovementReasonDto, ReasonSideDto, ScoreBasisDto } from '../../api/model';
import { compareSnapshotsResource, listSnapshotsResource } from '../../api/snapshots/snapshots.resource';
import { LoadState } from '../../shared/ui/load-state';
import { PageHeader } from '../../shared/ui/page-header';
import { formatScore, scoreBasis } from '../../shared/ui/score';
import { categoryLabel } from './selection';

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

const signed = (n: number): string => (n < 0 ? `−${-n}` : `+${n}`);
const ballotWord = (side: ReasonSideDto): string => side.ballot?.toUpperCase() ?? 'none';

/** What differed for one vote between A and B, in words, and what it did to the state's weights. */
export function reasonText(reason: MovementReasonDto): string {
  const { a, b } = reason;
  let what: string;
  if (a === null || b === null) {
    const only = a === null ? 'B' : 'A';
    const side = (a ?? b)!;
    what = `only in ${only}, treated as ${categoryLabel(side.category)}${side.ballot ? ` with country vote ${ballotWord(side)}` : ''}`;
  } else {
    what = reason.changes
      .map((change) => {
        switch (change) {
          case 'category':
            return `category ${categoryLabel(a.category)} → ${categoryLabel(b.category)}`;
          case 'ballot':
            return `country vote ${ballotWord(a)} → ${ballotWord(b)}`;
          case 'stance':
            return `position ${a.stance} → ${b.stance}`;
          case 'weight':
            return `weight ${a.weight} → ${b.weight}`;
        }
      })
      .join(', ');
  }
  return `${what} · matched weight ${signed(reason.matchedChange)} · counted weight ${signed(reason.countedChange)}`;
}

/** Every state's rank and score in two snapshots and how far it moved, read from what they stored. */
@Component({
  selector: 'app-snapshot-movement',
  imports: [MatButtonModule, MatTableModule, LoadState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (comparison.hasValue() && comparison.value(); as c) {
      <div class="table-scroll">
        <table mat-table [dataSource]="c.states" data-testid="movement">
          <ng-container matColumnDef="state">
            <th mat-header-cell *matHeaderCellDef>Member state</th>
            <td mat-cell *matCellDef="let s" class="state">
              {{ s.stateName }} ({{ s.stateCode }})
              @if (s.reasons.length > 0) {
                <button type="button" mat-button [attr.data-testid]="'why-' + s.stateCode" [attr.aria-expanded]="isOpen(s.stateCode)"
                  (click)="toggled.emit(s.stateCode)">Why ({{ s.reasons.length }})</button>
                @if (isOpen(s.stateCode)) {
                  <ul class="reasons" [attr.data-testid]="'reasons-' + s.stateCode">
                    @for (r of s.reasons; track r.vote.voteId) {
                      <li>{{ r.vote.assemblyVoteId }} {{ r.title }}: {{ reasonText(r) }}</li>
                    }
                  </ul>
                }
              } @else if (s.rankChange) {
                <span class="note">Rank moved because other states’ scores moved.</span>
              }
            </td>
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
    .state {
      min-width: 200px;
    }
    .reasons {
      margin: 4px 0 8px;
      padding-inline-start: 20px;
      font: var(--mat-sys-body-small);
    }
    .note {
      display: block;
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
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
  /** The states whose reasons are open. */
  readonly open = input<readonly string[]>([]);
  readonly toggled = output<string>();

  protected isOpen(code: string): boolean {
    return this.open().includes(code);
  }

  protected readonly reasonText = reasonText;
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
          <app-snapshot-movement [a]="+a()!" [b]="+b()!" [open]="opened()" (toggled)="toggle($event)" />
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
  /** From <code>?open=</code>: the states whose reasons are open, comma separated. */
  readonly open = input<string>();

  protected readonly opened = computed(() => (this.open() ?? '').split(',').filter(Boolean));
  private readonly router = inject(Router);
  protected readonly snapshots = listSnapshotsResource(computed(() => ({ page: 1, pageSize: 200 })));
  protected readonly calculated = computed(() =>
    (this.snapshots.hasValue() ? this.snapshots.value()?.items ?? [] : []).filter((s) => s.job.state === JobState.done),
  );
  protected readonly sides = [
    { key: 'a' as const, value: computed(() => this.a() ?? '') },
    { key: 'b' as const, value: computed(() => this.b() ?? '') },
  ];

  protected toggle(code: string): void {
    const now = this.opened();
    const next = now.includes(code) ? now.filter((c) => c !== code) : [...now, code];
    void this.router.navigate([], { queryParams: { open: next.join(',') || null }, queryParamsHandling: 'merge', replaceUrl: true });
  }

  protected pick(side: 'a' | 'b', event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    void this.router.navigate([], { queryParams: { [side]: value || null }, queryParamsHandling: 'merge' });
  }
}

