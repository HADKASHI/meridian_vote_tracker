import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { MatPaginatorModule } from '@angular/material/paginator';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { listImportRowsResource, listImportVoteCountsResource } from '../../api/imports/imports.resource';
import { Outcome, OutcomeCountsDto } from '../../api/model';
import { OUTCOMES } from './import-counts';

const PAGE_SIZE = 100;

/** The report's audit view: counts per outcome as filters, counts per vote, and every row record paged. */
@Component({
  selector: 'app-import-rows',
  imports: [MatTableModule, MatPaginatorModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <nav class="filters" data-testid="outcome-filters" aria-label="Outcomes">
      <a [routerLink]="[]" [queryParams]="{ outcome: null, page: null }" queryParamsHandling="merge"
         [attr.aria-current]="outcome() ? null : 'true'">All {{ total() }}</a>
      @for (o of outcomes; track o) {
        <a [routerLink]="[]" [queryParams]="{ outcome: o, page: null }" queryParamsHandling="merge"
           [attr.aria-current]="outcome() === o ? 'true' : null">{{ o }} {{ counts()[o] }}</a>
      }
    </nav>

    @if (votes.hasValue() && votes.value(); as perVote) {
      @if (perVote.length > 0) {
        <h2>Counts per vote</h2>
        <table class="vote-counts" data-testid="vote-counts">
          <thead>
            <tr><th>Vote</th>@for (o of outcomes; track o) {<th>{{ o }}</th>}</tr>
          </thead>
          <tbody>
            @for (v of perVote; track v.vote.voteId) {
              <tr [class.selected]="voteId() === v.vote.voteId">
                <td>
                  <a [routerLink]="[]" [queryParams]="{ voteId: voteId() === v.vote.voteId ? null : v.vote.voteId, page: null }"
                     queryParamsHandling="merge">{{ v.vote.assemblyVoteId }}</a>
                </td>
                @for (o of outcomes; track o) {<td>{{ v.counts[o] || '' }}</td>}
              </tr>
            }
          </tbody>
        </table>
      }
    }

    <h2>Rows</h2>
    @if (rows.hasValue() && rows.value(); as page) {
      <table mat-table [dataSource]="page.items" data-testid="rows">
        <ng-container matColumnDef="line">
          <th mat-header-cell *matHeaderCellDef>Line</th>
          <td mat-cell *matCellDef="let r">{{ r.lineNo }}</td>
        </ng-container>
        <ng-container matColumnDef="raw">
          <th mat-header-cell *matHeaderCellDef>Raw text</th>
          <td mat-cell *matCellDef="let r" class="raw">{{ r.rawText }}</td>
        </ng-container>
        <ng-container matColumnDef="outcome">
          <th mat-header-cell *matHeaderCellDef>Outcome</th>
          <td mat-cell *matCellDef="let r">{{ r.outcome }}</td>
        </ng-container>
        <ng-container matColumnDef="reason">
          <th mat-header-cell *matHeaderCellDef>Reason</th>
          <td mat-cell *matCellDef="let r">
            {{ r.reasonMessage }}
            @if (r.normalisations.length > 0) {
              <div class="normalisations">Normalised: {{ r.normalisations.join('; ') }}</div>
            }
          </td>
        </ng-container>
        <ng-container matColumnDef="links">
          <th mat-header-cell *matHeaderCellDef>Links</th>
          <td mat-cell *matCellDef="let r">
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
      @if (page.total === 0) {
        <p>No rows with this outcome.</p>
      } @else {
        <mat-paginator [length]="page.total" [pageSize]="pageSize" [pageIndex]="page.page - 1" [hidePageSize]="true"
                       (page)="goTo($event.pageIndex + 1)" />
      }
    }
  `,
  styles: `
    .filters {
      display: flex;
      flex-wrap: wrap;
      gap: 12px;
      margin-block: 12px;
    }
    .filters a[aria-current='true'] {
      font-weight: 600;
      text-decoration: none;
    }
    .vote-counts td,
    .vote-counts th {
      padding: 2px 8px;
      text-align: end;
    }
    .vote-counts .selected {
      background: var(--mat-sys-surface-container-high);
    }
    .raw {
      font-family: monospace;
      overflow-wrap: anywhere;
    }
    .normalisations {
      opacity: 0.7;
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
  protected readonly columns = ['line', 'raw', 'outcome', 'reason', 'links'];
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
