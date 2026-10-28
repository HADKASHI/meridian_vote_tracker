import { ChangeDetectionStrategy, Component, computed, inject, input, numberAttribute } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { getImportResource } from '../../api/imports/imports.resource';
import { Router } from '@angular/router';
import { JobState, Outcome } from '../../api/model';
import { pollWhile } from '../../shared/http/poll';
import { jobProgress } from '../../shared/ui/job-progress';
import { JobStateBadge } from '../../shared/ui/job-state-badge';
import { ImportRows } from './import-rows';

/** The import report: the file's evidence and its job, polled while the job is active. */
@Component({
  selector: 'app-import-page',
  imports: [DatePipe, DecimalPipe, JobStateBadge, ImportRows],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>Import report</h1>
    <p>Import #{{ importId() }}</p>
    @if (report.hasValue() && report.value(); as r) {
      <dl class="evidence">
        <dt>File</dt>
        <dd>{{ r.fileName }} ({{ r.sizeBytes | number }} bytes)</dd>
        <dt>SHA-256</dt>
        <dd class="hash">{{ r.sha256 }}</dd>
        <dt>Uploaded</dt>
        <dd>{{ r.uploadedBy }}, {{ r.uploadedAt | date: 'yyyy-MM-dd HH:mm:ss' }}</dd>
        <dt>Job</dt>
        <dd><app-job-state-badge [state]="r.job.state" /> {{ progress() }}</dd>
        @if (r.parse; as parse) {
          <dt>Parsed</dt>
          @if (parse.rejectedReason) {
            <dd class="rejected">Rejected: {{ parse.rejectedReason }} Nothing was applied.</dd>
          } @else {
            <dd>
              {{ parse.dataRows | number }} data rows, {{ parse.lineEndings.toUpperCase() }} line endings,
              {{ parse.hasBom ? 'with BOM' : 'no BOM' }}
            </dd>
          }
        }
      </dl>
      @if (r.warnings.length > 0) {
        <h2>Warnings</h2>
        <ul data-testid="warnings">
          @for (w of r.warnings; track w.id) {
            <li>
              @if (w.vote; as vote) {
                <strong>{{ vote.assemblyVoteId }}:</strong>
              }
              {{ w.message }}
            </li>
          }
        </ul>
      }
      @if (finished() && !r.parse?.rejectedReason) {
        <app-import-rows [importId]="r.id" [counts]="r.counts" [outcome]="outcome()" [voteId]="voteId()"
                         [page]="page() ?? 1" [pageChange]="goToPage" />
      }
    }
  `,
  styles: `
    .evidence {
      display: grid;
      grid-template-columns: max-content 1fr;
      gap: 4px 16px;
    }
    .rejected {
      color: var(--mat-sys-error);
    }
    .hash {
      font-family: monospace;
      overflow-wrap: anywhere;
    }
  `,
})
export class ImportPage {
  private readonly router = inject(Router);
  readonly importId = input.required<string>();
  /** Query params: the report's filters live in the URL. */
  readonly outcome = input<Outcome | undefined>();
  readonly voteId = input(undefined, { transform: (v: unknown) => (v == null ? undefined : numberAttribute(v)) });
  readonly page = input(undefined, { transform: (v: unknown) => (v == null ? undefined : numberAttribute(v, 1)) });
  protected readonly report = getImportResource(computed(() => Number(this.importId())));
  private readonly job = computed(() => (this.report.hasValue() ? this.report.value()?.job : undefined));
  protected readonly progress = computed(() => {
    const job = this.job();
    return job ? jobProgress(job) : '';
  });
  private readonly active = computed(() => {
    const state = this.job()?.state;
    return state === JobState.queued || state === JobState.running;
  });
  /** The rows are only read once the job has written them all. */
  protected readonly finished = computed(() => {
    const state = this.job()?.state;
    return state === JobState.done || state === JobState.failed;
  });
  protected readonly goToPage = (page: number): void =>
    void this.router.navigate([], { queryParams: { page }, queryParamsHandling: 'merge' });

  constructor() {
    pollWhile(this.report, 1000, this.active);
  }
}
