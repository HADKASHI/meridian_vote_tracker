import { ChangeDetectionStrategy, Component, computed, inject, input, linkedSignal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatTableModule } from '@angular/material/table';
import { Router, RouterLink } from '@angular/router';
import { searchVotesResource } from '../../api/votes/votes.resource';
import { SearchVotesParams } from '../../api/model';
import { LoadState } from '../../shared/ui/load-state';
import { PageHeader } from '../../shared/ui/page-header';
import { positionLabel } from './vote-labels';

/** The search as it lives in the URL: every filter and the page are query params. */
export interface VoteQuery {
  q?: string;
  session?: string;
  from?: string;
  to?: string;
  noPosition?: string;
  page?: string;
}

/** The URL's query params as the API's search params; empty filters are left out. */
export function searchParams(query: VoteQuery, pageSize: number): SearchVotesParams {
  const params: SearchVotesParams = { page: Math.max(Number(query.page) || 1, 1), pageSize };
  if (query.q?.trim()) params.q = query.q.trim();
  if (Number(query.session) > 0) params.session = Number(query.session);
  if (query.from) params.from = query.from;
  if (query.to) params.to = query.to;
  if (query.noPosition === 'true') params.noPosition = true;
  return params;
}

/** HIS-01: find any vote by assembly vote id or title, session, date range and "no position". */
@Component({
  selector: 'app-votes-page',
  imports: [
    FormsModule, RouterLink, MatButtonModule, MatCheckboxModule, MatFormFieldModule, MatInputModule,
    MatPaginatorModule, MatTableModule, PageHeader, LoadState,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header heading="Votes">
      <p pageLead>Find any vote by its assembly vote id or title, session, date range, or votes still without a position.</p>
    </app-page-header>
    <form class="filters surface-card" (ngSubmit)="search()" role="search" aria-label="Search votes">
      <mat-form-field class="wide" subscriptSizing="dynamic">
        <mat-label>Assembly vote id or title</mat-label>
        <input matInput name="q" [(ngModel)]="form().q" data-testid="votes-q" />
      </mat-form-field>
      <mat-form-field class="narrow" subscriptSizing="dynamic">
        <mat-label>Session</mat-label>
        <input matInput name="session" type="number" min="1" [(ngModel)]="form().session" />
      </mat-form-field>
      <mat-form-field class="narrow" subscriptSizing="dynamic">
        <mat-label>From</mat-label>
        <input matInput name="from" type="date" [(ngModel)]="form().from" />
      </mat-form-field>
      <mat-form-field class="narrow" subscriptSizing="dynamic">
        <mat-label>To</mat-label>
        <input matInput name="to" type="date" [(ngModel)]="form().to" />
      </mat-form-field>
      <mat-checkbox name="noPosition" [(ngModel)]="form().noPosition">No position</mat-checkbox>
      <button mat-flat-button type="submit">Search</button>
    </form>

    @if (votes.hasValue() && votes.value(); as result) {
      @if (result.items.length === 0) {
        <app-load-state kind="empty" message="No votes match." />
      } @else {
      <div class="table-scroll">
      <table mat-table [dataSource]="result.items" data-testid="votes-table">
        <ng-container matColumnDef="vote">
          <th mat-header-cell *matHeaderCellDef>Vote</th>
          <td mat-cell *matCellDef="let vote" class="nowrap"><a [routerLink]="['/votes', vote.id]">{{ vote.assemblyVoteId }}</a></td>
        </ng-container>
        <ng-container matColumnDef="session">
          <th mat-header-cell *matHeaderCellDef>Session</th>
          <td mat-cell *matCellDef="let vote">{{ vote.session }}</td>
        </ng-container>
        <ng-container matColumnDef="date">
          <th mat-header-cell *matHeaderCellDef>Date</th>
          <td mat-cell *matCellDef="let vote" class="nowrap">{{ vote.voteDate }}</td>
        </ng-container>
        <ng-container matColumnDef="title">
          <th mat-header-cell *matHeaderCellDef>Title</th>
          <td mat-cell *matCellDef="let vote" class="title">{{ vote.title }}</td>
        </ng-container>
        <ng-container matColumnDef="position">
          <th mat-header-cell *matHeaderCellDef>Position</th>
          <td mat-cell *matCellDef="let vote" class="nowrap">
            @if (vote.position) {
              {{ position(vote.position) }}
            } @else {
              <span class="chip" data-tone="outline">{{ position(vote.position) }}</span>
            }
          </td>
        </ng-container>
        <tr mat-header-row *matHeaderRowDef="columns"></tr>
        <tr mat-row *matRowDef="let row; columns: columns"></tr>
      </table>
      </div>
      <mat-paginator
        [length]="result.total"
        [pageIndex]="result.page - 1"
        [pageSize]="result.pageSize"
        [hidePageSize]="true"
        (page)="goToPage($event)"
      />
      }
    } @else if (votes.error()) {
      <app-load-state kind="error" message="The votes could not be loaded." />
    } @else {
      <app-load-state kind="loading" message="Searching votes" />
    }
  `,
  styles: `
    .filters {
      display: flex;
      flex-wrap: wrap;
      gap: 12px;
      align-items: center;
    }
    .wide {
      flex: 1 1 260px;
    }
    .narrow {
      flex: 1 1 140px;
      max-width: 200px;
    }
    @media (max-width: 600px) {
      .narrow {
        max-width: none;
      }
    }
    .nowrap {
      white-space: nowrap;
    }
    .title {
      min-width: 220px;
    }
  `,
})
export class VotesPage {
  readonly q = input<string>();
  readonly session = input<string>();
  readonly from = input<string>();
  readonly to = input<string>();
  readonly noPosition = input<string>();
  readonly page = input<string>();

  private readonly router = inject(Router);
  protected readonly columns = ['vote', 'session', 'date', 'title', 'position'];
  private readonly query = computed<VoteQuery>(() => ({
    q: this.q(), session: this.session(), from: this.from(), to: this.to(),
    noPosition: this.noPosition(), page: this.page(),
  }));
  protected readonly votes = searchVotesResource(computed(() => searchParams(this.query(), 50)));
  protected readonly position = positionLabel;

  /** The form's working copy, reset from the URL; the URL only changes on Search. */
  protected readonly form = linkedSignal(() => {
    const query = this.query();
    return {
      q: query.q ?? '', session: query.session ?? '', from: query.from ?? '', to: query.to ?? '',
      noPosition: query.noPosition === 'true',
    };
  });

  protected search(): void {
    const form = this.form();
    void this.router.navigate([], {
      queryParams: {
        q: form.q.trim() || null,
        session: form.session || null,
        from: form.from || null,
        to: form.to || null,
        noPosition: form.noPosition ? 'true' : null,
        page: null,
      },
    });
  }

  protected goToPage(event: PageEvent): void {
    void this.router.navigate([], { queryParams: { page: event.pageIndex + 1 }, queryParamsHandling: 'merge' });
  }
}
