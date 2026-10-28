import { ChangeDetectionStrategy, Component, computed, inject, input, linkedSignal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { Router, RouterLink } from '@angular/router';
import { getConflictResource } from '../../api/conflicts/conflicts.resource';
import { ConflictsService } from '../../api/conflicts/conflicts.service';
import { AlreadyDecidedProblem, ConflictDecisionDto, ConflictDto, ConflictValueDto, Decision } from '../../api/model';
import { ConflictCard } from '../../shared/problem/conflict-card';
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
  imports: [DatePipe, RouterLink, MatButtonModule, MatFormFieldModule, MatInputModule, ProblemBanner, ConflictCard],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <article data-testid="decision-pane">
      <h2>Conflict #{{ conflictId() }}</h2>
      @if (conflict(); as c) {
        <p class="about">
          <a [routerLink]="['/votes', c.vote.voteId]">{{ c.vote.assemblyVoteId }}</a> · {{ subject(c) }}
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
          <section data-testid="incoming">
            <h3>Incoming</h3>
            <p class="value">{{ value(c, c.incoming.value) }}</p>
            <p class="origin">{{ origin(c.incoming.origin) }}</p>
          </section>
        </div>
        @if (c.decision; as d) {
          <section class="decided" data-testid="decision">
            <p>
              <strong>{{ decision(d.decision) }} by {{ d.decidedBy }}</strong> at {{ d.decidedAt | date: 'yyyy-MM-dd HH:mm' }}:
              <span class="value">{{ value(c, d.from) }} → {{ value(c, d.to) }}</span>
            </p>
            @if (d.comment) {
              <blockquote>{{ d.comment }}</blockquote>
            }
          </section>
        }
        @if (lost(); as winner) {
          <app-conflict-card heading="Someone else decided first" [traceId]="lostTraceId()" data-testid="already-decided">
            <p>
              <strong>{{ winner.decidedBy }} {{ winner.decision === 'accept' ? 'accepted' : 'rejected' }}</strong>
              at {{ winner.decidedAt | date: 'yyyy-MM-dd HH:mm:ss' }}:
              <span class="value">{{ value(c, winner.from) }} → {{ value(c, winner.to) }}</span>
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
        <p>This conflict could not be loaded.</p>
      }
    </article>
  `,
  styles: `
    .sides {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 16px;
    }
    .sides section {
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: 8px;
      padding: 8px 16px;
    }
    h3 {
      margin-block: 0 4px;
      font-size: 0.95em;
      opacity: 0.8;
    }
    .value {
      font-family: monospace;
      font-size: 1.1em;
    }
    .origin,
    .not-applied {
      opacity: 0.75;
      font-size: 0.9em;
    }
    .decided {
      margin-block-start: 16px;
    }
    .decide {
      margin-block-start: 16px;
    }
    .decide mat-form-field {
      width: 100%;
    }
    .actions {
      display: flex;
      gap: 8px;
      align-items: center;
      margin-block-start: 12px;
    }
    .field-error {
      color: var(--mat-sys-error);
      margin-block: 4px 0;
    }
    .none-left {
      opacity: 0.75;
    }
    blockquote {
      margin: 4px 0 0;
      padding-inline-start: 12px;
      border-inline-start: 3px solid var(--mat-sys-outline-variant);
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
    if (!next) {
      this.noneLeft.set(true);
      return;
    }
    void this.router.navigate(['/review', next.id], { queryParams: { state: this.inbox.filter() } });
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
