import { ChangeDetectionStrategy, Component, computed, inject, input, linkedSignal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { Router, RouterLink } from '@angular/router';
import { getConflictResource } from '../../api/conflicts/conflicts.resource';
import { ConflictsService } from '../../api/conflicts/conflicts.service';
import { AlreadyDecidedProblem, ConflictDecisionDto, ConflictDto, ConflictFilter, ConflictValueDto, Decision } from '../../api/model';
import { ConflictCard } from '../../shared/problem/conflict-card';
import { FromTo } from '../../shared/ui/from-to';
import { LoadState } from '../../shared/ui/load-state';
import { ProblemBanner } from '../../shared/problem/problem-banner';
import { isProblem, Problem, ProblemType, toProblem } from '../../shared/problem/problem';
import { problemToFieldErrors } from '../../shared/problem/to-field-errors';
import { decisionLabel, originLabel, subjectLabel, valueLabel } from './conflict-values';
import { ReviewInbox } from './review-inbox';

/** The longest comment the API takes. */
const COMMENT_MAX = 2000;

type CommentField = 'comment';

/** The decision pane: both values with their origins, so the analyst decides on evidence. */
@Component({
  selector: 'app-conflict-page',
  imports: [DatePipe, RouterLink, MatButtonModule, MatFormFieldModule, MatInputModule, ProblemBanner, ConflictCard, LoadState, FromTo],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <article data-testid="decision-pane">
      <h2>Conflict #{{ conflictId() }}</h2>
      @if (conflict(); as c) {
        <p class="about">
          <a [routerLink]="['/votes', c.vote.voteId]">{{ c.vote.assemblyVoteId }}</a>
          <span class="chip" [attr.data-tone]="c.subject === 'details' ? 'tertiary' : 'primary'">{{ subject(c) }}</span>
        </p>
        <div class="sides">
          <section data-testid="current">
            <h3>Current</h3>
            @if (c.current; as current) {
              <p class="value">{{ value(c, current.value) }}</p>
              <p class="origin">{{ origin(current.origin) }}</p>
            } @else {
              <p class="value">—</p>
            }
          </section>
          <section data-testid="incoming" class="incoming">
            <h3>Incoming</h3>
            <p class="value">{{ value(c, c.incoming.value) }}</p>
            <p class="origin">{{ origin(c.incoming.origin) }}</p>
          </section>
        </div>
        @if (c.decision; as d) {
          <section class="decided" data-testid="decision" role="status">
            <p>
              <strong>{{ decision(d.decision) }} by {{ d.decidedBy }}</strong> at {{ d.decidedAt | date: 'yyyy-MM-dd HH:mm' }}:
              <app-from-to class="value" [from]="value(c, d.from)" [to]="value(c, d.to)" />
            </p>
            @if (d.comment) {
              <blockquote>{{ d.comment }}</blockquote>
            }
            @if (d.membership; as m) {
              <div class="membership" data-testid="membership">
                <h3>Membership recalculated for the vote</h3>
                @if (m.approvedStateCodes.length === 0 && m.noLongerCountedStateCodes.length === 0) {
                  <p>No country vote changed.</p>
                }
                @if (m.approvedStateCodes.length > 0) {
                  <p>Country votes approved by this decision: {{ m.approvedStateCodes.join(', ') }}</p>
                }
                @if (m.noLongerCountedStateCodes.length > 0) {
                  <p>No longer counted (outside membership on the new date): {{ m.noLongerCountedStateCodes.join(', ') }}</p>
                }
                @if (m.openedConflictIds.length > 0) {
                  <p>
                    Conflicts opened:
                    @for (id of m.openedConflictIds; track id) {
                      <a [routerLink]="['/review', id]">#{{ id }}</a>{{ $last ? '' : ', ' }}
                    }
                  </p>
                }
              </div>
            }
          </section>
        }
        @if (lost(); as winner) {
          <app-conflict-card heading="Someone else decided first" [traceId]="lostTraceId()" data-testid="already-decided">
            <p>
              <strong>{{ winner.decidedBy }} {{ decision(winner.decision).toLowerCase() }}</strong>
              at {{ winner.decidedAt | date: 'yyyy-MM-dd HH:mm:ss' }}:
              <app-from-to class="value" [from]="value(c, winner.from)" [to]="value(c, winner.to)" />
            </p>
            @if (winner.comment) {
              <blockquote>{{ winner.comment }}</blockquote>
            }
            <p>Your decision was not applied and is on the vote's timeline.</p>
            <div cardActions>
              <button mat-stroked-button type="button" (click)="next()">Next conflict</button>
              @if (noneLeft()) {
                <span class="none-left">No more pending conflicts.</span>
              }
            </div>
          </app-conflict-card>
        } @else if (!c.decision) {
          <form class="decide" (submit)="$event.preventDefault()">
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>Comment (optional)</mat-label>
              <textarea matInput rows="3" [maxLength]="commentMax" [value]="comment()"
                (input)="comment.set($any($event.target).value)"></textarea>
            </mat-form-field>
            @if (commentError(); as message) {
              <p class="field-error" role="alert" data-testid="comment-error">{{ message }}</p>
            }
            <app-problem-banner [messages]="formErrors()" [traceId]="problem()?.traceId" />
            <div class="actions">
              <button mat-flat-button type="button" [disabled]="deciding()" (click)="decide(c, 'accept')">Accept incoming</button>
              <button mat-stroked-button type="button" [disabled]="deciding()" (click)="decide(c, 'reject')">Keep current</button>
            </div>
          </form>
        } @else {
          <div class="actions">
            <button mat-stroked-button type="button" (click)="next()">Next conflict</button>
            @if (noneLeft()) {
              <span class="none-left">No more pending conflicts.</span>
            }
          </div>
        }
        @if (c.notApplied.length > 0) {
          <ul class="not-applied" data-testid="not-applied">
            @for (a of c.notApplied; track a.id) {
              <li>{{ a.attemptedBy }} tried to {{ a.attempted }}; not applied ({{ a.attemptedAt | date: 'yyyy-MM-dd HH:mm:ss' }})</li>
            }
          </ul>
        }
      } @else if (resource.error()) {
        <app-load-state kind="error" message="This conflict could not be loaded." />
      } @else {
        <app-load-state kind="loading" message="Loading the conflict" />
      }
    </article>
  `,
  styles: `
    h2 {
      font: var(--mat-sys-title-large);
      margin: 0;
    }
    .about {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 8px;
      margin-block: 4px 16px;
      font-family: var(--app-mono);
    }
    .sides {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
      gap: 12px;
    }
    .sides section {
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: 12px;
      padding: 12px 16px;
      background: var(--mat-sys-surface-container-low);
      overflow-wrap: anywhere;
    }
    .sides .incoming {
      border-color: var(--mat-sys-primary);
    }
    h3 {
      margin-block: 0 4px;
      font: var(--mat-sys-label-large);
      color: var(--mat-sys-on-surface-variant);
    }
    .value {
      font: var(--mat-sys-title-medium);
      margin-block: 0 4px;
    }
    .origin,
    .not-applied {
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
      margin: 0;
    }
    .decided {
      margin-block-start: 16px;
      padding: 12px 16px;
      border-radius: 12px;
      background: var(--mat-sys-secondary-container);
      color: var(--mat-sys-on-secondary-container);
      overflow-wrap: anywhere;
    }
    .decided p {
      margin-block: 0 4px;
    }
    .decided .value {
      font: inherit;
      font-family: var(--app-mono);
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
      align-items: center;
      margin-block-start: 12px;
    }
    .field-error {
      color: var(--mat-sys-error);
      margin-block: 4px 0;
    }
    .none-left {
      color: var(--mat-sys-on-surface-variant);
    }
    .membership {
      margin-block-start: 12px;
    }
    .membership h3 {
      color: inherit;
    }
    .membership p {
      margin-block: 2px;
    }
    .not-applied {
      margin-block-start: 12px;
      padding-inline-start: 20px;
    }
    blockquote {
      margin: 4px 0 0;
      padding-inline-start: 12px;
      border-inline-start: 3px solid currentColor;
      font-style: italic;
    }
  `,
})
export class ConflictPage {
  readonly conflictId = input.required<string>();
  protected readonly resource = getConflictResource(computed(() => Number(this.conflictId())));
  protected readonly conflict = computed<ConflictDto | undefined>(() =>
    this.resource.hasValue() ? this.resource.value() : undefined,
  );
  protected readonly subject = subjectLabel;
  protected readonly origin = originLabel;
  protected readonly decision = decisionLabel;
  protected readonly commentMax = COMMENT_MAX;

  private readonly inbox = inject(ReviewInbox);
  private readonly conflicts = inject(ConflictsService);
  private readonly router = inject(Router);

  // The pane is reused when "Next conflict" changes the id, so its own state follows the id.
  protected readonly comment = linkedSignal({ source: this.conflictId, computation: () => '' });
  protected readonly deciding = linkedSignal({ source: this.conflictId, computation: () => false });
  protected readonly problem = linkedSignal<string, Problem | null>({ source: this.conflictId, computation: () => null });
  /** The winner's decision when this analyst lost a race (409 already-decided). */
  protected readonly lost = linkedSignal<string, ConflictDecisionDto | null>({ source: this.conflictId, computation: () => null });
  protected readonly lostTraceId = linkedSignal<string, string | undefined>({ source: this.conflictId, computation: () => undefined });
  protected readonly noneLeft = linkedSignal({ source: this.conflictId, computation: () => false });
  /** Inbox order when this conflict was decided, so "Next conflict" moves on from here. */
  private readonly order = linkedSignal<string, number[] | null>({ source: this.conflictId, computation: () => null });

  private readonly errors = computed(() =>
    problemToFieldErrors<CommentField>(this.problem(), (path) => (path === 'comment' ? 'comment' : undefined)),
  );
  protected readonly commentError = computed(() => this.errors().fieldErrors.find((e) => e.field === 'comment')?.message);
  protected readonly formErrors = computed(() => this.errors().formErrors);

  protected decide(c: ConflictDto, decision: Decision): void {
    this.deciding.set(true);
    this.problem.set(null);
    this.order.set(this.inbox.orderAfter(c.id));
    this.conflicts.decideConflict(c.id, { decision, comment: this.comment().trim() || null }).subscribe({
      next: () => this.afterDecision(),
      error: (error: unknown) => {
        this.deciding.set(false);
        const problem = toProblem(error);
        if (isProblem<AlreadyDecidedProblem>(problem, ProblemType.alreadyDecided)) {
          // The card replaces the buttons; never a toast, never a retry.
          this.lost.set(problem.decision);
          this.lostTraceId.set(problem.traceId);
          this.inbox.list.reload();
        } else {
          this.problem.set(problem);
        }
      },
    });
  }

  protected next(): void {
    const id = Number(this.conflictId());
    const next = this.inbox.nextPending(this.order() ?? this.inbox.orderAfter(id), id);
    if (next) {
      void this.router.navigate(['/review', next.id], { queryParams: this.inbox.queryParams() });
    } else if (this.inbox.filter() === ConflictFilter.pending && this.inbox.total() <= this.inbox.items().length) {
      this.noneLeft.set(true);
    } else {
      // The list shows decided conflicts, or not all pending ones: ask for the first pending one.
      const pending = { ...this.inbox.queryParams(), state: ConflictFilter.pending };
      this.conflicts.listConflicts({ ...pending, pageSize: 2 }).subscribe((page) => {
        const first = page.items.find((item) => item.id !== id);
        if (first) void this.router.navigate(['/review', first.id], { queryParams: pending });
        else this.noneLeft.set(true);
      });
    }
  }

  private afterDecision(): void {
    this.deciding.set(false);
    this.resource.reload();
    this.inbox.list.reload();
  }

  protected value(c: ConflictDto, value: ConflictValueDto | null): string {
    return valueLabel(c.subject, value);
  }
}
