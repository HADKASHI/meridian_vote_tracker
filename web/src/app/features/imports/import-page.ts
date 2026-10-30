import { ChangeDetectionStrategy, Component, computed, inject, input, numberAttribute } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { getImportResource } from '../../api/imports/imports.resource';
import { Router, RouterLink } from '@angular/router';
import { JobState, Outcome } from '../../api/model';
import { pollWhile } from '../../shared/http/poll';
import { jobProgress } from '../../shared/ui/job-progress';
import { ExpandableList } from '../../shared/ui/expandable-list';
import { JobStateBadge } from '../../shared/ui/job-state-badge';
import { LoadState } from '../../shared/ui/load-state';
import { FileDownload } from '../../shared/ui/file-download';
import { PageHeader } from '../../shared/ui/page-header';
import { ImportRows } from './import-rows';

/** The import report: the file's evidence and its job, polled while the job is active. */
@Component({
  selector: 'app-import-page',
  imports: [DatePipe, DecimalPipe, RouterLink, MatProgressBarModule, JobStateBadge, ImportRows, PageHeader, LoadState, ExpandableList, FileDownload],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @let r = report.hasValue() ? report.value() : undefined;
    <app-page-header [heading]="r ? 'Import #' + r.id + ' · ' + r.fileName : 'Import #' + importId()"
                     [back]="{ label: 'Imports', link: '/imports' }">
      @if (r) {
        <p pageLead class="lead">
          Uploaded by {{ r.uploadedBy }} at {{ r.uploadedAt | date: 'yyyy-MM-dd HH:mm:ss' }} · Job
          <app-job-state-badge [state]="r.job.state" /> {{ progress() }}
        </p>
      }
      @if (r) {
        <div pageActions>
          <app-file-download [url]="'/api/imports/' + r.id + '/file'" [fileName]="r.fileName" />
        </div>
      }
    </app-page-header>
    @if (r) {
      @if (active()) {
        <mat-progress-bar [mode]="percent() === null ? 'indeterminate' : 'determinate'" [value]="percent() ?? 0"
                          aria-label="Import progress" class="progress" />
      }
      <details class="surface-card evidence-card" open>
        <summary>File evidence</summary>
        <dl class="evidence">
          <dt>File</dt>
          <dd>{{ r.fileName }} ({{ r.sizeBytes | number }} bytes)</dd>
          <dt>SHA-256</dt>
          <dd class="hash">{{ r.sha256 }}</dd>
          @if (r.parse; as parse) {
            <dt>Parsed</dt>
            @if (!parse.rejectedReason) {
              <dd>
                {{ parse.dataRows | number }} data rows, {{ parse.lineEndings.toUpperCase() }} line endings,
                {{ parse.hasBom ? 'with BOM' : 'no BOM' }}
              </dd>
            } @else {
              <dd>Rejected</dd>
            }
          }
        </dl>
      </details>
      @if (r.parse?.rejectedReason; as reason) {
        <p class="notice" data-tone="error" role="alert">Rejected: {{ reason }} Nothing was applied.</p>
      }
      @if (r.conflictsOpened.length > 0) {
        <section class="notice" aria-labelledby="conflicts-heading">
          <h2 id="conflicts-heading">
            {{ r.conflictsOpened.length }} {{ r.conflictsOpened.length === 1 ? 'conflict' : 'conflicts' }} opened
          </h2>
          <ul data-testid="conflicts-opened">
            @for (c of r.conflictsOpened; track c.conflictId) {
              <li>
                <a [routerLink]="['/review', c.conflictId]">Conflict #{{ c.conflictId }}</a>
                {{ c.vote.assemblyVoteId }} · {{ c.subject === 'details' ? 'vote details' : c.stateCode + ' ballot' }} ·
                {{ c.rows }} {{ c.rows === 1 ? 'row' : 'rows' }}
              </li>
            }
          </ul>
        </section>
      }
      @if (r.warnings.length > 0) {
        <section class="notice" data-tone="warning" aria-labelledby="warnings-heading">
          <h2 id="warnings-heading">Warnings</h2>
          <ul data-testid="warnings">
            @for (w of r.warnings; track w.id) {
              <li>
                @if (w.vote; as vote) {
                  <a [routerLink]="['/votes', vote.voteId]"><strong>{{ vote.assemblyVoteId }}</strong></a>:
                }
                {{ w.message }}
                @if (w.missingStateCodes?.length) {
                  <span class="missing">Missing: <app-expandable-list [items]="w.missingStateCodes!" noun="missing states" /></span>
                }
              </li>
            }
          </ul>
        </section>
      }
      @if (finished() && !r.parse?.rejectedReason) {
        <app-import-rows [importId]="r.id" [counts]="r.counts" [outcome]="outcome()" [voteId]="voteId()"
                         [page]="page() ?? 1" [pageChange]="goToPage" />
      }
    } @else if (report.error()) {
      <app-load-state kind="error" message="This import could not be loaded." />
    } @else {
      <app-load-state kind="loading" message="Loading the import" />
    }
  `,
  styles: `
    .lead {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 4px 6px;
    }
    .progress {
      margin-block-end: 16px;
    }
    summary {
      cursor: pointer;
      font: var(--mat-sys-title-small);
    }
    .evidence {
      display: grid;
      grid-template-columns: max-content minmax(0, 1fr);
      gap: 4px 16px;
      margin-block: 12px 0;
    }
    .evidence dt {
      color: var(--mat-sys-on-surface-variant);
    }
    .evidence dd {
      margin: 0;
      overflow-wrap: anywhere;
    }
    .hash {
      font-family: var(--app-mono);
    }
    .notice h2 {
      font: var(--mat-sys-title-small);
      margin: 0 0 4px;
    }
    .missing {
      display: block;
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
  /** How far the job is, in percent, once it knows how many votes it has. */
  protected readonly percent = computed(() => {
    const job = this.job();
    return job?.votesTotal ? (100 * job.votesDone) / job.votesTotal : null;
  });
  protected readonly active = computed(() => {
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
