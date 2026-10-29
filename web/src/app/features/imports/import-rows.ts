import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { MatPaginatorModule } from '@angular/material/paginator';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { listImportRowsResource, listImportVoteCountsResource } from '../../api/imports/imports.resource';
import { Outcome, OutcomeCountsDto } from '../../api/model';
import { LoadState } from '../../shared/ui/load-state';
import { OutcomeChip } from '../../shared/ui/outcome-chip';
import { OUTCOMES } from './import-counts';

const PAGE_SIZE = 100;

/** The report's audit view: counts per outcome as filters, counts per vote, and every row record paged. */
@Component({
  selector: 'app-import-rows',
  imports: [MatTableModule, MatPaginatorModule, RouterLink, LoadState, OutcomeChip],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <nav class="filters" data-testid="outcome-filters" aria-label="Outcomes">
      <a class="filter" [routerLink]="[]" [queryParams]="{ outcome: null, page: null }" queryParamsHandling="merge"
         [attr.aria-current]="outcome() ? null : 'true'">All&ngsp;<strong>{{ total() }}</strong></a>
      @for (o of outcomes; track o) {
        <a class="filter" [attr.data-outcome]="o" [routerLink]="[]" [queryParams]="{ outcome: o, page: null }"
           queryParamsHandling="merge" [attr.aria-current]="outcome() === o ? 'true' : null"
           >{{ o }}&ngsp;<strong>{{ counts()[o] }}</strong></a>
      }
    </nav>

    @if (votes.hasValue() && votes.value(); as perVote) {
      @if (perVote.length > 0) {
        <h2>Counts per vote</h2>
        <div class="table-scroll">
          <table class="vote-counts" data-testid="vote-counts">
            <thead>
              <tr><th scope="col">Vote</th>@for (o of outcomes; track o) {<th scope="col">{{ o }}</th>}</tr>
            </thead>
            <tbody>
              @for (v of perVote; track v.vote.voteId) {
                <tr [class.selected]="voteId() === v.vote.voteId">
                  <th scope="row">
                    <a [routerLink]="[]" [queryParams]="{ voteId: voteId() === v.vote.voteId ? null : v.vote.voteId, page: null }"
                       queryParamsHandling="merge" [attr.aria-current]="voteId() === v.vote.voteId ? 'true' : null"
                       >{{ v.vote.assemblyVoteId }}</a>
                  </th>
                  @for (o of outcomes; track o) {<td>{{ v.counts[o] || '' }}</td>}
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
    }

    <h2>Rows</h2>
    @if (rows.hasValue() && rows.value(); as page) {
      @if (page.total === 0) {
        <app-load-state kind="empty" message="No rows with this outcome." />
      } @else {
        <div class="table-scroll">
          <table mat-table [dataSource]="page.items" data-testid="rows">
            <ng-container matColumnDef="line">
              <th mat-header-cell *matHeaderCellDef>Line</th>
              <td mat-cell *matCellDef="let r" class="line">{{ r.lineNo }}</td>
            </ng-container>
            <ng-container matColumnDef="outcome">
              <th mat-header-cell *matHeaderCellDef>Outcome</th>
              <td mat-cell *matCellDef="let r"><app-outcome-chip [outcome]="r.outcome" /></td>
            </ng-container>
            <ng-container matColumnDef="raw">
              <th mat-header-cell *matHeaderCellDef>Raw text</th>
              <td mat-cell *matCellDef="let r" class="raw">{{ r.rawText }}</td>
            </ng-container>
            <ng-container matColumnDef="reason">
              <th mat-header-cell *matHeaderCellDef>Reason</th>
              <td mat-cell *matCellDef="let r" class="reason">
                {{ r.reasonMessage }}
                @if (r.normalisations.length > 0) {
                  <div class="normalisations">Normalised: {{ r.normalisations.join('; ') }}</div>
                }
              </td>
            </ng-container>
            <ng-container matColumnDef="links">
              <th mat-header-cell *matHeaderCellDef>Links</th>
              <td mat-cell *matCellDef="let r" class="links">
                @if (r.vote) {
                  <a [routerLink]="['/votes', r.vote.voteId]">{{ r.vote.assemblyVoteId }}</a>
                  @if (r.stateCode) { · {{ r.stateCode }} }
                }
                @for (c of r.conflictIds; track c) {
                  · <a [routerLink]="['/review', c]">Conflict #{{ c }}</a>
                }
              </td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="columns"></tr>
            <tr mat-row *matRowDef="let row; columns: columns"></tr>
          </table>
        </div>
        <mat-paginator [length]="page.total" [pageSize]="pageSize" [pageIndex]="page.page - 1" [hidePageSize]="true"
                       (page)="goTo($event.pageIndex + 1)" />
      }
    } @else if (rows.error()) {
      <app-load-state kind="error" message="The rows could not be loaded." />
    } @else {
      <app-load-state kind="loading" message="Loading rows" />
    }
  `,
  styles: `
    .filters {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
      margin-block: 8px 16px;
    }
    .filter {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      padding: 6px 14px;
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: 8px;
      background: var(--mat-sys-surface);
      color: var(--mat-sys-on-surface);
      text-decoration: none;
      font: var(--mat-sys-label-large);
    }
    .filter:hover {
      background: var(--mat-sys-surface-container-high);
    }
    /* The chosen filter: a check mark and a fill, not colour alone. */
    .filter[aria-current='true'] {
      background: var(--mat-sys-secondary-container);
      color: var(--mat-sys-on-secondary-container);
      border-color: transparent;
    }
    .filter[aria-current='true']::before {
      content: '✓';
    }
    .vote-counts {
      border-collapse: collapse;
    }
    .vote-counts th,
    .vote-counts td {
      padding: 8px 12px;
      text-align: end;
      border-block-end: 1px solid var(--mat-sys-outline-variant);
      font-variant-numeric: tabular-nums;
    }
    .vote-counts thead th {
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-label-large);
    }
    .vote-counts th[scope='row'],
    .vote-counts thead th:first-child {
      text-align: start;
    }
    .vote-counts tbody tr:last-child > * {
      border-block-end: none;
    }
    .vote-counts .selected {
      background: var(--mat-sys-secondary-container);
    }
    .line {
      font-variant-numeric: tabular-nums;
    }
    .raw {
      font-family: var(--app-mono);
      font-size: 0.85em;
      overflow-wrap: anywhere;
      min-width: 220px;
    }
    .reason {
      min-width: 200px;
    }
    .links {
      min-width: 120px;
    }
    .normalisations {
      color: var(--mat-sys-on-surface-variant);
    }
  `,
})
export class ImportRows {
  readonly importId = input.required<number>();
  readonly counts = input.required<OutcomeCountsDto>();
  readonly outcome = input<Outcome | undefined>();
  readonly voteId = input<number | undefined>();
  readonly page = input(1);
  readonly pageChange = input<(page: number) => void>(() => undefined);

  protected readonly outcomes = OUTCOMES;
  protected readonly pageSize = PAGE_SIZE;
  protected readonly columns = ['line', 'outcome', 'raw', 'reason', 'links'];
  protected readonly total = computed(() => OUTCOMES.reduce((sum, o) => sum + this.counts()[o], 0));
  protected readonly votes = listImportVoteCountsResource(this.importId);
  protected readonly rows = listImportRowsResource(
    this.importId,
    computed(() => ({ outcome: this.outcome(), voteId: this.voteId(), page: this.page(), pageSize: PAGE_SIZE })),
  );

  protected goTo(page: number): void {
    this.pageChange()(page);
  }
}
