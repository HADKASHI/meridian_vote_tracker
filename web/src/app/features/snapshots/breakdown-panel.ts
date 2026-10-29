import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { getSnapshotBreakdownResource } from '../../api/snapshots/snapshots.resource';
import { categoryLabel, formatScore } from './selection';

/** SNP-03: one state's breakdown, every vote with its category and the exact value used, to recompute by hand. */
@Component({
  selector: 'app-breakdown-panel',
  imports: [MatTableModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (breakdown.hasValue() && breakdown.value(); as b) {
      <h2>{{ b.result.stateName }} ({{ b.result.stateCode }})</h2>
      <p data-testid="breakdown-result">
        Score {{ score(b.result.score) }} · {{ b.result.matchedWeight }} of {{ b.result.countedWeight }} weight matched over
        {{ b.result.countableVotes }} countable {{ b.result.countableVotes === 1 ? 'vote' : 'votes' }}
      </p>
      <table mat-table [dataSource]="b.lines" data-testid="breakdown-lines">
        <ng-container matColumnDef="vote">
          <th mat-header-cell *matHeaderCellDef>Vote</th>
          <td mat-cell *matCellDef="let l">
            <a [routerLink]="['/votes', l.vote.voteId]">{{ l.vote.assemblyVoteId }}</a>
          </td>
        </ng-container>
        <ng-container matColumnDef="date">
          <th mat-header-cell *matHeaderCellDef>Date</th>
          <td mat-cell *matCellDef="let l">{{ l.voteDate }}</td>
        </ng-container>
        <ng-container matColumnDef="title">
          <th mat-header-cell *matHeaderCellDef>Title</th>
          <td mat-cell *matCellDef="let l">{{ l.title }}</td>
        </ng-container>
        <ng-container matColumnDef="position">
          <th mat-header-cell *matHeaderCellDef>Position</th>
          <td mat-cell *matCellDef="let l">{{ l.stance }}, weight {{ l.weight }}</td>
        </ng-container>
        <ng-container matColumnDef="ballot">
          <th mat-header-cell *matHeaderCellDef>Country vote</th>
          <td mat-cell *matCellDef="let l">{{ l.ballot ? l.ballot.toUpperCase() : '–' }}</td>
        </ng-container>
        <ng-container matColumnDef="category">
          <th mat-header-cell *matHeaderCellDef>Category</th>
          <td mat-cell *matCellDef="let l">{{ label(l.category) }}</td>
        </ng-container>
        <ng-container matColumnDef="revision">
          <th mat-header-cell *matHeaderCellDef>Revision used</th>
          <td mat-cell *matCellDef="let l">{{ l.countryVoteRevisionId ?? 'none' }}</td>
        </ng-container>
        <tr mat-header-row *matHeaderRowDef="columns"></tr>
        <tr mat-row *matRowDef="let row; columns: columns"></tr>
      </table>
    } @else if (breakdown.error()) {
      <p role="alert">This member state is not in the snapshot.</p>
    }
  `,
})
export class BreakdownPanel {
  readonly snapshotId = input.required<number>();
  readonly stateCode = input.required<string>();

  protected readonly columns = ['vote', 'date', 'title', 'position', 'ballot', 'category', 'revision'];
  protected readonly breakdown = getSnapshotBreakdownResource(this.snapshotId, this.stateCode);

  protected score(score: number | null): string {
    return formatScore(score);
  }

  protected label(category: string): string {
    return categoryLabel(category);
  }
}
