import { ChangeDetectionStrategy, Component, booleanAttribute, computed, inject, input, numberAttribute } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTableModule } from '@angular/material/table';
import { Router, RouterLink } from '@angular/router';
import { listPositionsResource } from '../../api/positions/positions.resource';
import { PositionDto } from '../../api/model';

/** The positions worklist: every vote with its stance and weight, votes without a position first. */
@Component({
  selector: 'app-positions-page',
  imports: [MatTableModule, MatPaginatorModule, MatSlideToggleModule, DatePipe, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>Positions</h1>
    <mat-slide-toggle [checked]="noPosition()" (change)="navigate({ noPosition: $event.checked || null, page: null })">
      No position only
    </mat-slide-toggle>
    @if (positions.hasValue() && positions.value(); as result) {
      <table mat-table [dataSource]="result.items" data-testid="positions-table">
        <ng-container matColumnDef="vote">
          <th mat-header-cell *matHeaderCellDef>Vote</th>
          <td mat-cell *matCellDef="let item">
            <a [routerLink]="['/votes', item.vote.voteId]">{{ item.vote.assemblyVoteId }}</a>
          </td>
        </ng-container>
        <ng-container matColumnDef="date">
          <th mat-header-cell *matHeaderCellDef>Date</th>
          <td mat-cell *matCellDef="let item">{{ item.voteDate }}</td>
        </ng-container>
        <ng-container matColumnDef="session">
          <th mat-header-cell *matHeaderCellDef>Session</th>
          <td mat-cell *matCellDef="let item">{{ item.session }}</td>
        </ng-container>
        <ng-container matColumnDef="title">
          <th mat-header-cell *matHeaderCellDef>Title</th>
          <td mat-cell *matCellDef="let item">{{ item.title }}</td>
        </ng-container>
        <ng-container matColumnDef="position">
          <th mat-header-cell *matHeaderCellDef>Position</th>
          <td mat-cell *matCellDef="let item" data-testid="position">
            @if (item.position; as position) {
              {{ describe(position) }}
              <span class="set-by">· {{ position.setBy }}, {{ position.setAt | date: 'yyyy-MM-dd HH:mm' }}</span>
            } @else {
              <span class="none">No position</span>
            }
          </td>
        </ng-container>
        <tr mat-header-row *matHeaderRowDef="columns"></tr>
        <tr mat-row *matRowDef="let row; columns: columns"></tr>
      </table>
      @if (result.items.length === 0) {
        <p>{{ noPosition() ? 'Every vote has a position.' : 'No votes yet.' }}</p>
      }
      <mat-paginator
        [length]="result.total"
        [pageIndex]="result.page - 1"
        [pageSize]="result.pageSize"
        [pageSizeOptions]="[25, 50, 100]"
        (page)="onPage($event)"
      />
    }
  `,
  styles: `
    .none {
      font-style: italic;
      opacity: 0.8;
    }
    .set-by {
      opacity: 0.7;
    }
  `,
})
export class PositionsPage {
  /** Query params, bound by the router. */
  readonly noPosition = input(false, { transform: booleanAttribute });
  readonly page = input(1, { transform: (v: unknown) => numberAttribute(v, 1) });
  readonly pageSize = input(50, { transform: (v: unknown) => numberAttribute(v, 50) });

  private readonly router = inject(Router);
  protected readonly columns = ['vote', 'date', 'session', 'title', 'position'];
  protected readonly params = computed(() => ({
    noPosition: this.noPosition() || undefined,
    page: this.page(),
    pageSize: this.pageSize(),
  }));
  protected readonly positions = listPositionsResource(this.params);

  protected describe(position: PositionDto): string {
    return positionLabel(position);
  }

  protected onPage(event: PageEvent): void {
    this.navigate({ page: event.pageIndex + 1, pageSize: event.pageSize });
  }

  protected navigate(queryParams: Record<string, unknown>): void {
    void this.router.navigate([], { queryParams, queryParamsHandling: 'merge' });
  }
}

/** "Yes · weight 4": a position in the words the worklist and its 409 card share. */
export function positionLabel(position: Pick<PositionDto, 'stance' | 'weight'>): string {
  return `${position.stance === 'yes' ? 'Yes' : 'No'} · weight ${position.weight}`;
}
