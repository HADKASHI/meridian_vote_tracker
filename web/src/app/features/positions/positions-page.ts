import { ChangeDetectionStrategy, Component, booleanAttribute, computed, inject, input, numberAttribute } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTableModule } from '@angular/material/table';
import { Router, RouterLink } from '@angular/router';
import { listPositionsResource } from '../../api/positions/positions.resource';
import { PositionDto, PositionWorklistItemDto } from '../../api/model';
import { LoadState } from '../../shared/ui/load-state';
import { PageHeader } from '../../shared/ui/page-header';
import { PositionEditor } from './position-editor';
import { positionLabel, setByName } from './position-label';
import { CurrentUser } from '../../core/identity/current-user';

/** The positions worklist: every vote with its stance and weight, votes without a position first. */
@Component({
  selector: 'app-positions-page',
  imports: [MatTableModule, MatPaginatorModule, MatSlideToggleModule, DatePipe, RouterLink, PositionEditor, PageHeader, LoadState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header heading="Positions">
      <p pageLead>What the Institute wanted in each vote and how much it counts. Votes without a position come first;
        only an admin sets them.</p>
    </app-page-header>
    <div class="toolbar surface-card">
      <mat-slide-toggle [checked]="noPosition()" (change)="navigate({ noPosition: $event.checked || null, page: null })">
        No position only
      </mat-slide-toggle>
    </div>
    @if (positions.hasValue() && positions.value(); as result) {
      @if (result.items.length === 0) {
        <app-load-state kind="empty" [message]="noPosition() ? 'Every vote has a position.' : 'No votes yet.'" />
      } @else {
      <div class="table-scroll">
      <table mat-table [dataSource]="result.items" [trackBy]="byVote" data-testid="positions-table">
        <ng-container matColumnDef="vote">
          <th mat-header-cell *matHeaderCellDef>Vote</th>
          <td mat-cell *matCellDef="let item" class="nowrap">
            <a [routerLink]="['/votes', item.vote.voteId]">{{ item.vote.assemblyVoteId }}</a>
          </td>
        </ng-container>
        <ng-container matColumnDef="date">
          <th mat-header-cell *matHeaderCellDef>Date</th>
          <td mat-cell *matCellDef="let item" class="nowrap">{{ item.voteDate }}</td>
        </ng-container>
        <ng-container matColumnDef="session">
          <th mat-header-cell *matHeaderCellDef>Session</th>
          <td mat-cell *matCellDef="let item">{{ item.session }}</td>
        </ng-container>
        <ng-container matColumnDef="title">
          <th mat-header-cell *matHeaderCellDef>Title</th>
          <td mat-cell *matCellDef="let item" class="title">{{ item.title }}</td>
        </ng-container>
        <ng-container matColumnDef="position">
          <th mat-header-cell *matHeaderCellDef>Position</th>
          <td mat-cell *matCellDef="let item" data-testid="position" class="position">
            @if (item.position; as position) {
              <strong>{{ describe(position) }}</strong>
              <span class="set-by">{{ setBy(position) }}, {{ position.setAt | date: 'yyyy-MM-dd HH:mm' }}</span>
            } @else {
              <span class="chip" data-tone="error">No position</span>
            }
          </td>
        </ng-container>
        <ng-container matColumnDef="edit">
          <th mat-header-cell *matHeaderCellDef>Set stance and weight</th>
          <td mat-cell *matCellDef="let item" class="edit">
            <app-position-editor [voteId]="item.vote.voteId" [position]="item.position" (saved)="positions.reload()" />
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
        [pageSizeOptions]="[25, 50, 100]"
        (page)="onPage($event)"
      />
      }
    } @else if (positions.error()) {
      <app-load-state kind="error" message="The positions could not be loaded." />
    } @else {
      <app-load-state kind="loading" message="Loading positions" />
    }
  `,
  styles: `
    .toolbar {
      padding-block: 12px;
    }
    .nowrap {
      white-space: nowrap;
    }
    .title {
      min-width: 200px;
    }
    .position {
      min-width: 140px;
    }
    .set-by {
      display: block;
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
    }
    .edit {
      min-width: 300px;
      padding-block: 8px;
    }
    /* On a phone the worklist keeps what the editing needs; the date and session are on the vote page. */
    @media (max-width: 600px) {
      .mat-column-date,
      .mat-column-session {
        display: none;
      }
    }
  `,
})
export class PositionsPage {
  /** Query params, bound by the router. */
  readonly noPosition = input(false, { transform: booleanAttribute });
  readonly page = input(1, { transform: (v: unknown) => numberAttribute(v, 1) });
  readonly pageSize = input(50, { transform: (v: unknown) => numberAttribute(v, 50) });

  private readonly router = inject(Router);
  private readonly users = inject(CurrentUser).users;
  protected readonly columns = ['vote', 'date', 'session', 'title', 'position', 'edit'];
  protected readonly params = computed(() => ({
    noPosition: this.noPosition() || undefined,
    page: this.page(),
    pageSize: this.pageSize(),
  }));
  protected readonly positions = listPositionsResource(this.params);
  /** Rows keep their editor across reloads, so an open edit or 409 card on one row survives another row's save. */
  protected readonly byVote = (_: number, item: PositionWorklistItemDto) => item.vote.voteId;

  protected describe(position: PositionDto): string {
    return positionLabel(position);
  }

  protected setBy(position: PositionDto): string {
    return setByName(this.users.hasValue() ? this.users.value() : [], position);
  }

  protected onPage(event: PageEvent): void {
    this.navigate({ page: event.pageIndex + 1, pageSize: event.pageSize });
  }

  protected navigate(queryParams: Record<string, unknown>): void {
    void this.router.navigate([], { queryParams, queryParamsHandling: 'merge' });
  }
}
