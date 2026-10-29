import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { MatTableModule } from '@angular/material/table';
import { MatTabsModule } from '@angular/material/tabs';
import { Router, RouterLink } from '@angular/router';
import { getVoteResource } from '../../api/votes/votes.resource';
import { LoadState } from '../../shared/ui/load-state';
import { PageHeader } from '../../shared/ui/page-header';
import { Origin } from './origin';
import { VoteTimeline } from './vote-timeline';
import { ballotLabel, positionLabel } from './vote-labels';

/** HIS-01 to HIS-04: a vote's approved values with their origins, and its timeline (`?tab=timeline`). */
@Component({
  selector: 'app-vote-page',
  imports: [MatTableModule, MatTabsModule, RouterLink, Origin, VoteTimeline, PageHeader, LoadState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (notFound()) {
      <app-page-header heading="Vote not found" [back]="{ label: 'Votes', link: '/votes' }" />
      <p class="surface-card">There is no vote #{{ voteId() }}. <a routerLink="/votes">Search votes</a></p>
    } @else if (vote.hasValue() && vote.value(); as v) {
      <app-page-header [heading]="v.assemblyVoteId" [back]="{ label: 'Votes', link: '/votes' }">
        @if (v.details; as details) {
          <p pageLead class="lead">{{ details.title }}</p>
        }
      </app-page-header>
      @if (v.pendingConflicts > 0) {
        <p class="notice" data-tone="warning" data-testid="vote-pending">
          {{ v.pendingConflicts }} pending {{ v.pendingConflicts === 1 ? 'conflict' : 'conflicts' }}: approved values stay
          until someone decides. <a routerLink="/review">Open the Review inbox</a>
        </p>
      }
      <mat-tab-group [selectedIndex]="tab() === 'timeline' ? 1 : 0" (selectedIndexChange)="selectTab($event)"
                     mat-stretch-tabs="false" animationDuration="0ms">
        <mat-tab label="Details">
          <div class="tab-body">
            <section class="surface-card">
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
                <p class="muted">No approved vote details: every row for this vote so far was a contradiction.</p>
              }
            </section>

            <h2>Country votes</h2>
            <div class="table-scroll">
              <table mat-table [dataSource]="v.countryVotes" data-testid="country-votes">
                <ng-container matColumnDef="state">
                  <th mat-header-cell *matHeaderCellDef>Member state</th>
                  <td mat-cell *matCellDef="let c" class="state"><span class="mono">{{ c.stateCode }}</span> {{ c.stateName }}</td>
                </ng-container>
                <ng-container matColumnDef="ballot">
                  <th mat-header-cell *matHeaderCellDef>Country vote</th>
                  <td mat-cell *matCellDef="let c" class="ballot">
                    {{ ballot(c.ballot) }}
                    @if (!c.memberOnVoteDate) {
                      <span class="chip" data-tone="outline">not a member on the vote date</span>
                    }
                  </td>
                </ng-container>
                <ng-container matColumnDef="origin">
                  <th mat-header-cell *matHeaderCellDef>Origin</th>
                  <td mat-cell *matCellDef="let c" class="origin">
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
            </div>
          </div>
        </mat-tab>
        <mat-tab label="Timeline">
          <ng-template matTabContent>
            <div class="tab-body">
              <app-vote-timeline [voteId]="v.id" />
            </div>
          </ng-template>
        </mat-tab>
      </mat-tab-group>
    } @else if (vote.error()) {
      <app-page-header heading="Vote" [back]="{ label: 'Votes', link: '/votes' }" />
      <app-load-state kind="error" [message]="'Vote #' + voteId() + ' could not be loaded.'" />
    } @else {
      <app-page-header heading="Vote" [back]="{ label: 'Votes', link: '/votes' }" />
      <p class="muted">Loading vote #{{ voteId() }}…</p>
      <app-load-state kind="loading" message="Loading the vote" />
    }
  `,
  styles: `
    .lead {
      overflow-wrap: anywhere;
    }
    .tab-body {
      padding-block-start: 16px;
    }
    .details {
      display: grid;
      grid-template-columns: max-content minmax(0, 1fr);
      gap: 8px 24px;
      margin: 0;
    }
    .details dt {
      color: var(--mat-sys-on-surface-variant);
    }
    .details dd {
      margin: 0;
      overflow-wrap: anywhere;
    }
    .state {
      white-space: nowrap;
    }
    .ballot {
      min-width: 140px;
    }
    .ballot .chip {
      margin-inline-start: 6px;
      white-space: normal;
    }
    .origin {
      min-width: 240px;
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
  private readonly router = inject(Router);

  protected selectTab(index: number): void {
    void this.router.navigate([], { queryParams: { tab: index === 1 ? 'timeline' : null }, queryParamsHandling: 'merge' });
  }
}
