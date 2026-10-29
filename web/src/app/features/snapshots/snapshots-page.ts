import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { JobState, PublicationDto } from '../../api/model';
import { listSnapshotsResource } from '../../api/snapshots/snapshots.resource';
import { pollWhile } from '../../shared/http/poll';
import { JobStateBadge } from '../../shared/ui/job-state-badge';
import { LoadState } from '../../shared/ui/load-state';
import { PageHeader } from '../../shared/ui/page-header';
import { publicationBadge } from './selection';

const PAGE_SIZE = 50;

/** SNP: every snapshot, newest first, with its vote count and publication badge. */
@Component({
  selector: 'app-snapshots-page',
  imports: [DatePipe, MatButtonModule, MatTableModule, RouterLink, JobStateBadge, PageHeader, LoadState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header heading="Snapshots">
      <p pageLead>Each snapshot saves an Alignment Index over chosen votes and never changes; an admin publishes one per quarter.</p>
      <a pageActions mat-stroked-button routerLink="/snapshots/compare">Compare snapshots</a>
      <a pageActions mat-flat-button routerLink="/snapshots/new">New snapshot</a>
    </app-page-header>
    @if (snapshots.hasValue() && snapshots.value(); as page) {
      @if (page.items.length === 0) {
        <app-load-state kind="empty" message="No snapshots yet." />
      } @else {
      <div class="table-scroll">
      <table mat-table [dataSource]="page.items" data-testid="snapshots-table">
        <ng-container matColumnDef="name">
          <th mat-header-cell *matHeaderCellDef>Name</th>
          <td mat-cell *matCellDef="let s" class="name"><a [routerLink]="['/snapshots', s.id]">{{ s.name }}</a></td>
        </ng-container>
        <ng-container matColumnDef="createdBy">
          <th mat-header-cell *matHeaderCellDef>By</th>
          <td mat-cell *matCellDef="let s">{{ s.createdBy }}</td>
        </ng-container>
        <ng-container matColumnDef="createdAt">
          <th mat-header-cell *matHeaderCellDef>Created</th>
          <td mat-cell *matCellDef="let s" class="nowrap">{{ s.createdAt | date: 'yyyy-MM-dd HH:mm' }}</td>
        </ng-container>
        <ng-container matColumnDef="votes">
          <th mat-header-cell *matHeaderCellDef>Votes</th>
          <td mat-cell *matCellDef="let s">{{ s.voteCount ?? '–' }}</td>
        </ng-container>
        <ng-container matColumnDef="state">
          <th mat-header-cell *matHeaderCellDef>Job</th>
          <td mat-cell *matCellDef="let s"><app-job-state-badge [state]="s.job.state" /></td>
        </ng-container>
        <ng-container matColumnDef="publication">
          <th mat-header-cell *matHeaderCellDef>Publication</th>
          <td mat-cell *matCellDef="let s">
            @if (badge(s.publications); as text) {
              <span class="chip" [attr.data-tone]="text.startsWith('Published') ? 'primary' : 'outline'">{{ text }}</span>
            }
          </td>
        </ng-container>
        <tr mat-header-row *matHeaderRowDef="columns"></tr>
        <tr mat-row *matRowDef="let row; columns: columns"></tr>
      </table>
      </div>
      }
      @if (page.total > page.pageSize) {
        <nav class="pager">
          <a mat-button [routerLink]="[]" [queryParams]="{ page: page.page - 1 }" [disabled]="page.page <= 1">Newer</a>
          <span>Page {{ page.page }} of {{ pages(page.total) }}</span>
          <a mat-button [routerLink]="[]" [queryParams]="{ page: page.page + 1 }" [disabled]="page.page >= pages(page.total)">
            Older
          </a>
        </nav>
      }
    } @else if (snapshots.error()) {
      <app-load-state kind="error" message="The snapshots could not be loaded." />
    } @else {
      <app-load-state kind="loading" message="Loading snapshots" />
    }
  `,
  styles: `
    .name {
      min-width: 160px;
      overflow-wrap: anywhere;
    }
    .nowrap {
      white-space: nowrap;
    }
    .pager {
      display: flex;
      align-items: center;
      gap: 8px;
    }
  `,
})
export class SnapshotsPage {
  /** From <code>?page=</code>. */
  readonly page = input<string>();

  protected readonly columns = ['name', 'createdBy', 'createdAt', 'votes', 'state', 'publication'];
  protected readonly snapshots = listSnapshotsResource(
    computed(() => ({ page: Math.max(Number(this.page() ?? 1) || 1, 1), pageSize: PAGE_SIZE })),
  );
  private readonly active = computed(() =>
    (this.snapshots.hasValue() ? this.snapshots.value()?.items ?? [] : []).some(
      (s) => s.job.state === JobState.queued || s.job.state === JobState.running,
    ),
  );

  constructor() {
    pollWhile(this.snapshots, 1000, this.active);
  }

  protected badge(publications: PublicationDto[]): string | null {
    return publicationBadge(publications);
  }

  protected pages(total: number): number {
    return Math.ceil(total / PAGE_SIZE);
  }
}
