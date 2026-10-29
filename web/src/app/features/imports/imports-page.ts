import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatTableModule } from '@angular/material/table';
import { Router, RouterLink } from '@angular/router';
import { ImportsService } from '../../api/imports/imports.service';
import { listImportsResource } from '../../api/imports/imports.resource';
import { JobState } from '../../api/model';
import { pollWhile } from '../../shared/http/poll';
import { Problem, toProblem } from '../../shared/problem/problem';
import { ProblemBanner } from '../../shared/problem/problem-banner';
import { JobStateBadge } from '../../shared/ui/job-state-badge';
import { countsSummary } from './import-counts';

/** Every import, newest first, and the upload that starts a new one. */
@Component({
  selector: 'app-imports-page',
  imports: [MatTableModule, MatButtonModule, DatePipe, RouterLink, JobStateBadge, ProblemBanner],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>Imports</h1>
    <input #picker type="file" accept=".csv,text/csv" hidden (change)="upload(picker)" />
    <button mat-flat-button type="button" [disabled]="uploading()" (click)="picker.click()">
      {{ uploading() ? 'Uploading…' : 'Upload a file' }}
    </button>
    @if (problem(); as p) {
      <app-problem-banner [messages]="[p.detail ?? p.title]" [traceId]="p.traceId" />
    }
    @if (imports.hasValue() && imports.value(); as page) {
      <table mat-table [dataSource]="page.items" data-testid="imports-table">
        <ng-container matColumnDef="file">
          <th mat-header-cell *matHeaderCellDef>File</th>
          <td mat-cell *matCellDef="let i">
            <a [routerLink]="['/imports', i.id]">#{{ i.id }} {{ i.fileName }}</a>
          </td>
        </ng-container>
        <ng-container matColumnDef="uploadedBy">
          <th mat-header-cell *matHeaderCellDef>Uploaded by</th>
          <td mat-cell *matCellDef="let i">{{ i.uploadedBy }}</td>
        </ng-container>
        <ng-container matColumnDef="uploadedAt">
          <th mat-header-cell *matHeaderCellDef>Uploaded at</th>
          <td mat-cell *matCellDef="let i">{{ i.uploadedAt | date: 'yyyy-MM-dd HH:mm:ss' }}</td>
        </ng-container>
        <ng-container matColumnDef="state">
          <th mat-header-cell *matHeaderCellDef>Job</th>
          <td mat-cell *matCellDef="let i"><app-job-state-badge [state]="i.job.state" /></td>
        </ng-container>
        <ng-container matColumnDef="outcomes">
          <th mat-header-cell *matHeaderCellDef>Outcomes</th>
          <td mat-cell *matCellDef="let i">{{ i.rejectedReason ? 'Rejected: ' + i.rejectedReason : summary(i.counts) }}</td>
        </ng-container>
        <tr mat-header-row *matHeaderRowDef="columns"></tr>
        <tr mat-row *matRowDef="let row; columns: columns"></tr>
      </table>
      @if (page.items.length === 0) {
        <p>No imports yet.</p>
      }
    }
  `,
})
export class ImportsPage {
  private readonly service = inject(ImportsService);
  private readonly router = inject(Router);
  protected readonly columns = ['file', 'uploadedBy', 'uploadedAt', 'state', 'outcomes'];
  protected readonly imports = listImportsResource(signal({ page: 1, pageSize: 50 }));
  protected readonly uploading = signal(false);
  protected readonly problem = signal<Problem | null>(null);
  protected readonly summary = countsSummary;
  private readonly active = computed(() =>
    (this.imports.hasValue() ? this.imports.value()?.items ?? [] : []).some(
      (i) => i.job.state === JobState.queued || i.job.state === JobState.running,
    ),
  );

  constructor() {
    pollWhile(this.imports, 1000, this.active);
  }

  protected upload(picker: HTMLInputElement): void {
    const file = picker.files?.[0];
    if (!file) return;
    this.uploading.set(true);
    this.problem.set(null);
    this.service.uploadImport({ file }).subscribe({
      next: (created) => void this.router.navigate(['/imports', created.importId]),
      error: (error: unknown) => {
        this.uploading.set(false);
        this.problem.set(toProblem(error));
        picker.value = '';
      },
    });
  }
}
