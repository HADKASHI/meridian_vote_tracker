import { ChangeDetectionStrategy, Component, inject, input, output, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { PublicationDto, QuarterPublishedProblem } from '../../api/model';
import { SnapshotsService } from '../../api/snapshots/snapshots.service';
import { CurrentUser } from '../../core/identity/current-user';
import { ConflictCard } from '../../shared/problem/conflict-card';
import { ProblemBanner } from '../../shared/problem/problem-banner';
import { isProblem, ProblemType, toProblem } from '../../shared/problem/problem';
import { problemToFieldErrors } from '../../shared/problem/to-field-errors';
import { quarterOf } from './selection';

/**
 * SNP-05: the admin publishes the snapshot for a reporting quarter. When the quarter is
 * already published, the 409 card shows that publication in place of the button and
 * offers "Replace it", which needs a reason.
 */
@Component({
  selector: 'app-publish-panel',
  imports: [DatePipe, MatButtonModule, MatFormFieldModule, MatInputModule, ConflictCard, ProblemBanner],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="panel" data-testid="publish-panel">
      <h2>Publish</h2>
      <div class="row">
        <mat-form-field>
          <mat-label>Reporting quarter</mat-label>
          <input matInput name="quarter" [value]="quarter()" (input)="setQuarter($event)" [disabled]="!currentUser.isAdmin()" />
          <mat-hint>{{ fieldErrors()['quarter'] ?? 'Like 2024-Q1; not tied to the dates of the votes' }}</mat-hint>
        </mat-form-field>
        @if (!taken()) {
          <button
            mat-flat-button
            type="button"
            [disabled]="!currentUser.isAdmin() || busy()"
            [attr.title]="currentUser.isAdmin() ? null : 'Admin only'"
            (click)="publish(null)"
          >
            Publish
          </button>
          @if (!currentUser.isAdmin()) {
            <span class="hint">Admin only</span>
          }
        }
      </div>

      @if (taken(); as current) {
        <app-conflict-card [heading]="current.quarter + ' is already published'" [traceId]="takenTraceId()">
          <p data-testid="quarter-published">
            {{ current.snapshotName }} (snapshot #{{ current.snapshotId }}), published by {{ current.publishedBy }} at
            {{ current.publishedAt | date: 'yyyy-MM-dd HH:mm' }}.
          </p>
          @if (replacing()) {
            <mat-form-field class="reason">
              <mat-label>Reason for replacing it</mat-label>
              <textarea matInput name="reason" rows="2" [value]="reason()" (input)="setReason($event)"></textarea>
              @if (fieldErrors()['reason']; as message) {
                <mat-hint class="field-error">{{ message }}</mat-hint>
              }
            </mat-form-field>
          }
          <div cardActions>
            @if (replacing()) {
              <button mat-flat-button type="button" [disabled]="busy()" (click)="publish(current.id)">Replace</button>
            } @else {
              <button mat-flat-button type="button" (click)="replacing.set(true)">Replace it</button>
            }
            <button mat-stroked-button type="button" (click)="reset()">Cancel</button>
          </div>
        </app-conflict-card>
      }
      <app-problem-banner [messages]="formErrors()" [traceId]="traceId()" />
    </section>
  `,
  styles: `
    .panel {
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: 12px;
      padding: 8px 16px 16px;
      margin-block: 16px;
      max-width: 720px;
    }
    .row {
      display: flex;
      gap: 16px;
      align-items: baseline;
    }
    .hint {
      opacity: 0.8;
    }
    .reason {
      width: 100%;
    }
    .field-error {
      color: var(--mat-sys-error);
    }
  `,
})
export class PublishPanel {
  readonly snapshotId = input.required<number>();
  readonly published = output<PublicationDto>();

  protected readonly currentUser = inject(CurrentUser);
  private readonly snapshots = inject(SnapshotsService);

  protected readonly quarter = signal(quarterOf(new Date()));
  protected readonly reason = signal('');
  protected readonly busy = signal(false);
  protected readonly taken = signal<PublicationDto | null>(null);
  protected readonly takenTraceId = signal<string | undefined>(undefined);
  protected readonly replacing = signal(false);
  protected readonly fieldErrors = signal<Record<string, string>>({});
  protected readonly formErrors = signal<string[]>([]);
  protected readonly traceId = signal<string | undefined>(undefined);

  protected setQuarter(event: Event): void {
    this.quarter.set((event.target as HTMLInputElement).value.trim());
    this.reset();
  }

  protected setReason(event: Event): void {
    this.reason.set((event.target as HTMLTextAreaElement).value);
  }

  protected reset(): void {
    this.taken.set(null);
    this.replacing.set(false);
    this.reason.set('');
    this.fieldErrors.set({});
    this.formErrors.set([]);
  }

  protected publish(supersedesPublicationId: number | null): void {
    this.busy.set(true);
    const request = supersedesPublicationId
      ? { quarter: this.quarter(), supersedesPublicationId, reason: this.reason() }
      : { quarter: this.quarter() };
    this.snapshots.publishSnapshot(this.snapshotId(), request).subscribe({
      next: (publication) => {
        this.busy.set(false);
        this.reset();
        this.published.emit(publication);
      },
      error: (error: unknown) => {
        this.busy.set(false);
        const problem = toProblem(error);
        if (isProblem<QuarterPublishedProblem>(problem, ProblemType.quarterPublished)) {
          // Someone published the quarter first, or replaced it while this was open: show theirs.
          this.taken.set(problem.publication);
          this.takenTraceId.set(problem.traceId);
          this.replacing.set(false);
          return;
        }
        const mapped = problemToFieldErrors(problem, (path) => (path === 'quarter' || path === 'reason' ? path : undefined));
        this.fieldErrors.set(Object.fromEntries(mapped.fieldErrors.map((e) => [e.field, e.message])));
        this.formErrors.set(problem ? mapped.formErrors : ['The server could not be reached.']);
        this.traceId.set(problem?.traceId);
      },
    });
  }
}
