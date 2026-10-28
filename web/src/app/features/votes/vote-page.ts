import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { getVoteResource } from '../../api/votes/votes.resource';
import { Origin } from './origin';
import { ballotLabel, positionLabel } from './vote-labels';

/** HIS-01, HIS-02: a vote's approved details, position and country votes, each value with its origin. */
@Component({
  selector: 'app-vote-page',
  imports: [MatTableModule, RouterLink, Origin],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (notFound()) {
      <h1>Vote not found</h1>
      <p>There is no vote #{{ voteId() }}. <a routerLink="/votes">Search votes</a></p>
    } @else if (vote.hasValue() && vote.value(); as v) {
      <h1>{{ v.assemblyVoteId }}</h1>
      @if (v.details; as details) {
        <dl class="details" data-testid="vote-details">
          <dt>Title</dt>
          <dd>{{ details.title }}</dd>
          <dt>Session</dt>
          <dd>{{ details.session }}</dd>
          <dt>Date</dt>
          <dd>{{ details.voteDate }}</dd>
          <dt>Origin</dt>
          <dd><app-origin [origin]="details.origin" /></dd>
          <dt>Position</dt>
          <dd data-testid="vote-position">
            {{ position(v.position) }}
            @if (v.position; as p) {
              <span class="muted">set by {{ p.setBy }}</span>
            }
          </dd>
        </dl>
      } @else {
        <p>No approved vote details: every row for this vote so far was a contradiction.</p>
      }
      @if (v.pendingConflicts > 0) {
        <p data-testid="vote-pending">
          {{ v.pendingConflicts }} pending {{ v.pendingConflicts === 1 ? 'conflict' : 'conflicts' }}.
          <a routerLink="/review">Open the Review inbox</a>
        </p>
      }

      <h2>Country votes</h2>
      <table mat-table [dataSource]="v.countryVotes" data-testid="country-votes">
        <ng-container matColumnDef="state">
          <th mat-header-cell *matHeaderCellDef>Member state</th>
          <td mat-cell *matCellDef="let c">{{ c.stateCode }} · {{ c.stateName }}</td>
        </ng-container>
        <ng-container matColumnDef="ballot">
          <th mat-header-cell *matHeaderCellDef>Country vote</th>
          <td mat-cell *matCellDef="let c">
            {{ ballot(c.ballot) }}
            @if (!c.memberOnVoteDate) {
              <span class="muted">not a member on the vote date</span>
            }
          </td>
        </ng-container>
        <ng-container matColumnDef="origin">
          <th mat-header-cell *matHeaderCellDef>Origin</th>
          <td mat-cell *matCellDef="let c">
            @if (c.origin) {
              <app-origin [origin]="c.origin" />
            } @else {
              <span class="muted">No approved value yet</span>
            }
          </td>
        </ng-container>
        <tr mat-header-row *matHeaderRowDef="columns"></tr>
        <tr mat-row *matRowDef="let row; columns: columns"></tr>
      </table>
    } @else {
      <h1>Vote</h1>
    }
  `,
  styles: `
    .details {
      display: grid;
      grid-template-columns: max-content 1fr;
      gap: 4px 16px;
    }
    .details dt {
      font-weight: 500;
    }
    .details dd {
      margin: 0;
    }
    .muted {
      opacity: 0.7;
    }
  `,
})
export class VotePage {
  readonly voteId = input.required<string>();
  readonly tab = input<string>();

  protected readonly vote = getVoteResource(computed(() => Number(this.voteId())));
  protected readonly notFound = computed(() => {
    const error = this.vote.error();
    return error instanceof HttpErrorResponse && error.status === 404;
  });
  protected readonly columns = ['state', 'ballot', 'origin'];
  protected readonly position = positionLabel;
  protected readonly ballot = ballotLabel;
}
