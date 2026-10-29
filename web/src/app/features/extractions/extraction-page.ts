import { ChangeDetectionStrategy, Component, computed, inject, input, linkedSignal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { RouterLink } from '@angular/router';
import { getExtractionResource } from '../../api/extractions/extractions.resource';
import { ExtractionsService } from '../../api/extractions/extractions.service';
import {
  Ballot, ExtractionAlreadyDecidedProblem, ExtractionDecisionDto, ExtractionDto, ExtractionStatus, SaveEditsRequest, SheetRow,
} from '../../api/model';
import { pollWhile } from '../../shared/http/poll';
import { isProblem, Problem, ProblemType, toProblem } from '../../shared/problem/problem';
import { ConflictCard } from '../../shared/problem/conflict-card';
import { ProblemBanner } from '../../shared/problem/problem-banner';
import { JobStateBadge } from '../../shared/ui/job-state-badge';
import { LoadState } from '../../shared/ui/load-state';
import { ReviewCounts } from '../../shared/ui/review-counts';
import { BALLOT_CHOICES, ballotLabel, isBlocking, statusLabel, statusTone, TOTAL_COLUMNS, TotalKey, totalOf } from './extraction-labels';
import { ExtractionsInbox } from './extractions-inbox';

/** The longest comment the API takes. */
const COMMENT_MAX = 2000;

type HeaderField = 'voteId' | 'session' | 'date' | 'title';
const HEADER_FIELDS: { key: HeaderField; label: string }[] = [
  { key: 'voteId', label: 'Vote number' },
  { key: 'session', label: 'Session' },
  { key: 'date', label: 'Date of vote' },
  { key: 'title', label: 'Resolution' },
];

/** A copy of `edits` without `key`. */
function without<T extends object>(edits: T, key: keyof T): T {
  const copy = { ...edits };
  delete copy[key];
  return copy;
}

interface RowEdits {
  code?: string;
  choice?: Ballot;
}

/**
 * One scanned sheet as the analyst checks it: what was read, the counts against the totals the sheet prints, what blocks
 * or warns, and the edits, approval and rejection. Edits are kept here until saved; Approve waits for them to be saved
 * and for every blocking issue to be gone.
 */
@Component({
  selector: 'app-extraction-page',
  imports: [DatePipe, RouterLink, MatButtonModule, MatFormFieldModule, MatInputModule, ProblemBanner, ConflictCard, LoadState, JobStateBadge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <article data-testid="extraction-pane">
      @if (extraction(); as e) {
        <h2>{{ e.fileName }}</h2>
        <p class="about">
          <span class="chip" data-testid="status" [attr.data-tone]="tone(e.status)">{{ label(e.status) }}</span>
          <span>Uploaded by {{ e.uploadedBy }} · {{ e.uploadedAt | date: 'yyyy-MM-dd HH:mm' }}</span>
        </p>

        @switch (e.status) {
          @case ('extracting') {
            <p class="reading" data-testid="reading" role="status">
              Reading the sheet… <app-job-state-badge [state]="e.job.state" />
            </p>
          }
          @case ('failed') {
            <div class="notice" data-tone="error" data-testid="failed" role="alert">
              <p><strong>The sheet could not be read.</strong> {{ e.error }}</p>
              <button mat-stroked-button type="button" data-testid="retry" [disabled]="busy()" (click)="retry(e)">Try again</button>
            </div>
          }
        }

        @if (e.decision; as d) {
          <section class="decided" data-testid="decision" role="status">
            <p>
              <strong>{{ label(d.status) }} by {{ d.decidedBy }}</strong> at {{ d.decidedAt | date: 'yyyy-MM-dd HH:mm' }}.
              @if (e.importId !== null) {
                <a [routerLink]="['/imports', e.importId]" data-testid="import-link">Import #{{ e.importId }}: see which rows were new, unchanged or in conflict.</a>
              }
            </p>
            @if (d.comment) {
              <blockquote>{{ d.comment }}</blockquote>
            }
          </section>
        }

        @if (e.header) {
          <section aria-label="The sheet's header">
            <h3>Header</h3>
            <div class="header-fields">
              @for (f of headerFields; track f.key) {
                <label>
                  <span>{{ f.label }}</span>
                  <input type="text" [attr.data-testid]="'header-' + f.key" [value]="headerValue(e, f.key)" [disabled]="!reviewable(e)"
                    (input)="setHeader(e, f.key, $any($event.target).value)" />
                </label>
              }
            </div>
          </section>

          <section aria-label="Counts against the printed totals">
            <h3>Counts against the totals the sheet prints</h3>
            <div class="table-scroll">
              <table class="totals" data-testid="totals">
                <thead>
                  <tr>
                    <th scope="col"><span class="visually-hidden">Source</span></th>
                    @for (c of totalColumns; track c.key) {
                      <th scope="col">{{ c.label }}</th>
                    }
                    <th scope="col">Total</th>
                  </tr>
                </thead>
                <tbody>
                  <tr data-testid="printed">
                    <th scope="row">Printed</th>
                    @for (c of totalColumns; track c.key) {
                      <td>
                        @if (reviewable(e)) {
                          <input type="number" min="0" step="1" [attr.aria-label]="'Printed ' + c.label" [attr.data-testid]="'printed-' + c.key"
                            [value]="printedValue(e, c.key)" (input)="setPrinted(e, c.key, $any($event.target).value)" />
                        } @else {
                          {{ e.printed ? e.printed[c.key] : '—' }}
                        }
                      </td>
                    }
                    <td>{{ e.printed ? total(e.printed) : '—' }}</td>
                  </tr>
                  <tr data-testid="counted">
                    <th scope="row">Counted from the rows</th>
                    @for (c of totalColumns; track c.key) {
                      <td [class.differs]="differs(e, c.key)">
                        {{ e.counted ? e.counted[c.key] : '—' }}
                        @if (differs(e, c.key)) {
                          <span class="visually-hidden"> (differs from the printed total)</span><span aria-hidden="true"> ≠</span>
                        }
                      </td>
                    }
                    <td>{{ e.counted ? total(e.counted) : '—' }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
            @if (hasPending()) {
              <p class="muted" data-testid="unsaved">You have unsaved edits; the counts update when you save them.</p>
            }
          </section>

          @if (e.issues.length > 0) {
            <section aria-label="Issues">
              <h3>Issues</h3>
              <ul class="issues" data-testid="issues">
                @for (issue of e.issues; track $index) {
                  <li>
                    <span class="chip" [attr.data-tone]="blocking(issue) ? 'error' : 'tertiary'">{{ blocking(issue) ? 'Blocks approval' : 'Warning' }}</span>
                    {{ issue.message }}
                  </li>
                }
              </ul>
            </section>
          }

          <section aria-label="Rows">
            <h3>Rows</h3>
            @if (rowsWithIssues(e).length > 0 || showAll()) {
              <button mat-button type="button" data-testid="toggle-rows" (click)="showAll.set(!showAll())">
                {{ showAll() ? 'Only rows with issues or edits' : 'Show all ' + e.rows.length + ' rows' }}
              </button>
            }
            <div class="table-scroll">
              <table class="rows" data-testid="rows">
                <thead>
                  <tr><th scope="col">Code</th><th scope="col">Member state</th><th scope="col">Ballot</th><th scope="col">Confidence</th><th scope="col">Issue</th></tr>
                </thead>
                <tbody>
                  @for (row of visibleRows(e); track row.position) {
                    <tr data-testid="row" [attr.data-code]="row.code" [class.edited]="edited(row)">
                      <td>
                        <input type="text" class="code" [attr.aria-label]="'Code of row ' + row.position" [value]="shownCode(row)"
                          [disabled]="!reviewable(e)" (input)="setCode(row, $any($event.target).value)" />
                      </td>
                      <td>{{ row.name }}</td>
                      <td>
                        <select [attr.aria-label]="'Ballot of ' + row.code" [value]="shownChoice(row) ?? ''" [disabled]="!reviewable(e)"
                          (change)="setChoice(row, $any($event.target).value)">
                          @if (shownChoice(row) === null) {
                            <option value="" selected>Two marks: choose</option>
                          }
                          @for (b of ballots; track b.value) {
                            <option [value]="b.value">{{ b.label }}</option>
                          }
                        </select>
                      </td>
                      <td [class.low]="row.confidence < 0.7">{{ row.confidence.toFixed(2) }}</td>
                      <td>
                        @for (issue of issuesOf(e, row); track $index) {
                          <span class="issue">{{ issue.message }}</span>
                        }
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </section>
        }

        @if (e.edits.length > 0) {
          <section aria-label="Edits so far">
            <h3>Edits so far</h3>
            <ul class="edits" data-testid="edits">
              @for (edit of e.edits; track $index) {
                <li>{{ edit.by }} changed {{ edit.field }}{{ edit.code ? ' of ' + edit.code : '' }}: {{ edit.from ?? '—' }} → {{ edit.to ?? '—' }}</li>
              }
            </ul>
          </section>
        }

        @if (lost(); as winner) {
          <app-conflict-card heading="Someone else decided first" [traceId]="lostTraceId()" data-testid="already-decided">
            <p><strong>{{ winner.decidedBy }} {{ label(winner.status).toLowerCase() }} this sheet</strong> at {{ winner.decidedAt | date: 'yyyy-MM-dd HH:mm:ss' }}.</p>
            @if (winner.comment) {
              <blockquote>{{ winner.comment }}</blockquote>
            }
            <p>Your change was not applied.</p>
          </app-conflict-card>
        } @else if (reviewable(e)) {
          <form class="decide" (submit)="$event.preventDefault()">
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>Comment (optional)</mat-label>
              <textarea matInput rows="2" [maxLength]="commentMax" [value]="comment()" (input)="comment.set($any($event.target).value)"></textarea>
            </mat-form-field>
            <app-problem-banner [messages]="messages()" [traceId]="problem()?.traceId" />
            <div class="actions">
              <button mat-stroked-button type="button" data-testid="save" [disabled]="busy() || !hasPending()" (click)="save(e)">Save edits</button>
              <button mat-flat-button type="button" data-testid="approve" [disabled]="busy() || hasPending() || !e.canApprove" (click)="approve(e)">Approve</button>
              <button mat-stroked-button type="button" data-testid="reject" [disabled]="busy()" (click)="reject(e)">Reject</button>
            </div>
            @if (!e.canApprove) {
              <p class="muted" data-testid="why-not">Fix or reject what blocks approval first.</p>
            } @else if (hasPending()) {
              <p class="muted" data-testid="why-not">Save your edits before approving.</p>
            }
          </form>
        }
      } @else if (resource.error()) {
        <app-load-state kind="error" message="This sheet could not be loaded." />
      } @else {
        <app-load-state kind="loading" message="Loading the sheet" />
      }
    </article>
  `,
  styles: `
    h2 {
      font: var(--mat-sys-title-large);
      margin: 0;
      overflow-wrap: anywhere;
    }
    h3 {
      margin-block: 16px 4px;
      font: var(--mat-sys-label-large);
      color: var(--mat-sys-on-surface-variant);
    }
    .about {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 8px;
      margin-block: 4px 12px;
      color: var(--mat-sys-on-surface-variant);
    }
    .header-fields {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
      gap: 8px 16px;
    }
    .header-fields label {
      display: grid;
      gap: 2px;
      font: var(--mat-sys-label-medium);
    }
    input[type='text'],
    input[type='number'],
    select {
      font: inherit;
      padding: 4px 8px;
      border: 1px solid var(--mat-sys-outline);
      border-radius: 6px;
      background: var(--mat-sys-surface);
      color: var(--mat-sys-on-surface);
      max-width: 100%;
    }
    input[type='number'] {
      width: 5em;
    }
    input.code {
      width: 5em;
      font-family: var(--app-mono);
    }
    table {
      border-collapse: collapse;
      width: 100%;
    }
    th,
    td {
      text-align: start;
      padding: 4px 8px;
      border-block-end: 1px solid var(--mat-sys-outline-variant);
      vertical-align: top;
    }
    .differs {
      color: var(--mat-sys-error);
      font-weight: 600;
    }
    .low {
      color: var(--mat-sys-error);
    }
    tr.edited {
      background: var(--mat-sys-secondary-container);
    }
    .issue {
      display: block;
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
    }
    .issues,
    .edits {
      margin: 0;
      padding-inline-start: 20px;
    }
    .issues li {
      margin-block: 4px;
    }
    .muted {
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
      margin-block: 4px 0;
    }
    .reading,
    .notice {
      margin-block: 12px;
    }
    .decided {
      margin-block: 12px;
      padding: 12px 16px;
      border-radius: 12px;
      background: var(--mat-sys-secondary-container);
      color: var(--mat-sys-on-secondary-container);
      overflow-wrap: anywhere;
    }
    .decided p {
      margin-block: 0 4px;
    }
    .decide {
      margin-block-start: 16px;
    }
    .decide mat-form-field {
      width: 100%;
    }
    .actions {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
      margin-block-start: 12px;
    }
    blockquote {
      margin: 4px 0 0;
      padding-inline-start: 12px;
      border-inline-start: 3px solid currentColor;
      font-style: italic;
    }
  `,
})
export class ExtractionPage {
  readonly extractionId = input.required<string>();
  protected readonly resource = getExtractionResource(computed(() => Number(this.extractionId())));
  protected readonly extraction = computed<ExtractionDto | undefined>(() => (this.resource.hasValue() ? this.resource.value() : undefined));

  private readonly service = inject(ExtractionsService);
  private readonly inbox = inject(ExtractionsInbox);
  private readonly counts = inject(ReviewCounts);

  protected readonly label = statusLabel;
  protected readonly tone = statusTone;
  protected readonly ballots = BALLOT_CHOICES;
  protected readonly totalColumns = TOTAL_COLUMNS;
  protected readonly headerFields = HEADER_FIELDS;
  protected readonly commentMax = COMMENT_MAX;
  protected readonly blocking = isBlocking;
  protected readonly total = totalOf;
  protected readonly ballotLabel = ballotLabel;

  // The pane is reused when another sheet is chosen, so its own state follows the id.
  protected readonly rowEdits = linkedSignal<string, Record<number, RowEdits>>({ source: this.extractionId, computation: () => ({}) });
  protected readonly headerEdits = linkedSignal<string, Partial<Record<HeaderField, string>>>({ source: this.extractionId, computation: () => ({}) });
  protected readonly printedEdits = linkedSignal<string, Partial<Record<TotalKey, string>>>({ source: this.extractionId, computation: () => ({}) });
  protected readonly comment = linkedSignal({ source: this.extractionId, computation: () => '' });
  protected readonly busy = linkedSignal({ source: this.extractionId, computation: () => false });
  protected readonly showAll = linkedSignal({ source: this.extractionId, computation: () => false });
  protected readonly problem = linkedSignal<string, Problem | null>({ source: this.extractionId, computation: () => null });
  protected readonly lost = linkedSignal<string, ExtractionDecisionDto | null>({ source: this.extractionId, computation: () => null });
  protected readonly lostTraceId = linkedSignal<string, string | undefined>({ source: this.extractionId, computation: () => undefined });

  protected readonly messages = computed(() => {
    const p = this.problem();
    if (!p) return [];
    const fields = Object.values(p.errors ?? {}).flat();
    return fields.length > 0 ? fields : [p.detail ?? p.title];
  });
  protected readonly hasPending = computed(
    () => Object.keys(this.rowEdits()).length + Object.keys(this.headerEdits()).length + Object.keys(this.printedEdits()).length > 0,
  );

  constructor() {
    pollWhile(this.resource, 1000, () => this.extraction()?.status === ExtractionStatus.extracting);
  }

  protected reviewable(e: ExtractionDto): boolean {
    return e.status === ExtractionStatus.needs_review && this.lost() === null;
  }

  protected headerValue(e: ExtractionDto, field: HeaderField): string {
    return this.headerEdits()[field] ?? e.header?.[field] ?? '';
  }

  protected printedValue(e: ExtractionDto, key: TotalKey): string {
    return this.printedEdits()[key] ?? (e.printed ? String(e.printed[key]) : '');
  }

  protected differs(e: ExtractionDto, key: TotalKey): boolean {
    return e.printed !== null && e.counted !== null && e.printed[key] !== e.counted[key];
  }

  protected shownCode(row: SheetRow): string {
    return this.rowEdits()[row.position]?.code ?? row.code;
  }

  protected shownChoice(row: SheetRow): Ballot {
    return this.rowEdits()[row.position]?.choice ?? row.choice;
  }

  protected edited(row: SheetRow): boolean {
    return this.rowEdits()[row.position] !== undefined;
  }

  protected issuesOf(e: ExtractionDto, row: SheetRow) {
    return e.issues.filter((i) => i.code === row.code);
  }

  protected rowsWithIssues(e: ExtractionDto): SheetRow[] {
    return e.rows.filter((row) => this.issuesOf(e, row).length > 0 || this.edited(row));
  }

  /** Rows with issues or edits, or every row when there are none or the analyst asked for them. */
  protected visibleRows(e: ExtractionDto): SheetRow[] {
    const some = this.rowsWithIssues(e);
    return this.showAll() || some.length === 0 ? e.rows : some;
  }

  protected setCode(row: SheetRow, value: string): void {
    const code = value.trim().toUpperCase();
    this.patchRow(row, { code: code === row.code ? undefined : code });
  }

  protected setChoice(row: SheetRow, value: string): void {
    const choice = BALLOT_CHOICES.find((b) => b.value === value)?.value;
    this.patchRow(row, { choice: choice === undefined || (choice === row.choice && !row.ambiguous) ? undefined : choice });
  }

  private patchRow(row: SheetRow, patch: RowEdits): void {
    this.rowEdits.update((edits) => {
      const next = { ...edits, [row.position]: { ...edits[row.position], ...patch } };
      const merged = next[row.position];
      if (merged.code === undefined && merged.choice === undefined) delete next[row.position];
      return next;
    });
  }

  protected setHeader(e: ExtractionDto, field: HeaderField, value: string): void {
    this.headerEdits.update((edits) => {
      const rest = without(edits, field);
      return value === (e.header?.[field] ?? '') ? rest : { ...rest, [field]: value };
    });
  }

  protected setPrinted(e: ExtractionDto, key: TotalKey, value: string): void {
    this.printedEdits.update((edits) => {
      const rest = without(edits, key);
      return value === (e.printed ? String(e.printed[key]) : '') ? rest : { ...rest, [key]: value };
    });
  }

  /** What to send: only what the analyst changed. Null when a printed total is not a whole number. */
  private request(e: ExtractionDto): SaveEditsRequest | null {
    const rows = Object.entries(this.rowEdits()).map(([position, edit]) => ({
      position: Number(position), code: edit.code ?? null, choice: edit.choice ?? null,
    }));
    const h = this.headerEdits();
    const header = Object.keys(h).length > 0 ? { voteId: h.voteId ?? null, session: h.session ?? null, date: h.date ?? null, title: h.title ?? null } : null;

    let printed = null;
    const p = this.printedEdits();
    if (Object.keys(p).length > 0) {
      const totals = { yes: e.printed?.yes ?? 0, no: e.printed?.no ?? 0, abstentions: e.printed?.abstentions ?? 0, notVoting: e.printed?.notVoting ?? 0 };
      for (const [key, text] of Object.entries(p) as [TotalKey, string][]) {
        const n = Number(text);
        if (text.trim() === '' || !Number.isInteger(n) || n < 0) {
          this.problem.set({ type: ProblemType.validation, title: 'Check the printed totals', status: 400, detail: 'Each printed total must be a whole number, 0 or more.' });
          return null;
        }
        totals[key] = n;
      }
      printed = totals;
    }
    return { rows: rows.length > 0 ? rows : null, header, printed };
  }

  protected save(e: ExtractionDto, then?: () => void): void {
    const request = this.request(e);
    if (!request) return;
    this.busy.set(true);
    this.problem.set(null);
    this.service.saveExtractionEdits(e.id, request).subscribe({
      next: () => {
        this.rowEdits.set({});
        this.headerEdits.set({});
        this.printedEdits.set({});
        this.busy.set(false);
        this.refresh();
        then?.();
      },
      error: (error: unknown) => this.failed(error),
    });
  }

  protected approve(e: ExtractionDto): void {
    this.busy.set(true);
    this.problem.set(null);
    this.service.approveExtraction(e.id, { comment: this.comment().trim() || null }).subscribe({
      next: () => {
        this.busy.set(false);
        this.refresh();
      },
      error: (error: unknown) => this.failed(error),
    });
  }

  protected reject(e: ExtractionDto): void {
    this.busy.set(true);
    this.problem.set(null);
    this.service.rejectExtraction(e.id, { comment: this.comment().trim() || null }).subscribe({
      next: () => {
        this.busy.set(false);
        this.refresh();
      },
      error: (error: unknown) => this.failed(error),
    });
  }

  protected retry(e: ExtractionDto): void {
    this.busy.set(true);
    this.problem.set(null);
    this.service.retryExtraction(e.id).subscribe({
      next: () => {
        this.busy.set(false);
        this.refresh();
      },
      error: (error: unknown) => this.failed(error),
    });
  }

  private failed(error: unknown): void {
    this.busy.set(false);
    const problem = toProblem(error);
    if (isProblem<ExtractionAlreadyDecidedProblem>(problem, ProblemType.alreadyDecided)) {
      // The card replaces the buttons; never a toast, never a retry.
      this.lost.set(problem.decision);
      this.lostTraceId.set(problem.traceId);
      this.refresh();
    } else {
      this.problem.set(problem ?? { type: ProblemType.unexpected, title: 'Something went wrong', status: 0 });
    }
  }

  private refresh(): void {
    this.resource.reload();
    this.inbox.list.reload();
    this.counts.reload();
  }
}
