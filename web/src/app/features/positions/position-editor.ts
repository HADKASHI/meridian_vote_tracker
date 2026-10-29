import { ChangeDetectionStrategy, Component, computed, inject, input, linkedSignal, output, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { DatePipe } from '@angular/common';
import { PositionsService } from '../../api/positions/positions.service';
import { PositionDto, Stance, StalePositionProblem } from '../../api/model';
import { CurrentUser } from '../../core/identity/current-user';
import { ConflictCard } from '../../shared/problem/conflict-card';
import { isProblem, ProblemType, toProblem } from '../../shared/problem/problem';
import { ProblemBanner } from '../../shared/problem/problem-banner';
import { problemToFieldErrors } from '../../shared/problem/to-field-errors';
import { positionLabel, setByName } from './position-label';

/**
 * One vote's stance and weight, edited inline. The save carries the revision the row
 * showed, so a position someone else saved meanwhile is never overwritten unseen.
 * Analysts see the controls disabled with the reason.
 */
@Component({
  selector: 'app-position-editor',
  imports: [MatButtonModule, MatButtonToggleModule, DatePipe, ConflictCard, ProblemBanner],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="editor">
      <mat-button-toggle-group
        [value]="stance()"
        (change)="stance.set($event.value)"
        [disabled]="!canEdit() || saving()"
        hideSingleSelectionIndicator
        aria-label="Stance"
      >
        <mat-button-toggle value="yes">Yes</mat-button-toggle>
        <mat-button-toggle value="no">No</mat-button-toggle>
      </mat-button-toggle-group>
      <select
        aria-label="Weight"
        data-testid="weight"
        [value]="weight() ?? ''"
        (change)="weight.set(toWeight($event))"
        [disabled]="!canEdit() || saving()"
      >
        <!-- Each option marks itself: the select's value is bound before its options exist. -->
        <option value="" disabled [selected]="weight() === null">Weight</option>
        @for (w of weights; track w) {
          <option [value]="w" [selected]="w === weight()">{{ w }}</option>
        }
      </select>
      @if (!stale()) {
        <button mat-stroked-button type="button" data-testid="save" [disabled]="!canSave()" (click)="save()">Save</button>
      }
      @if (!canEdit()) {
        <span class="reason">Admin only</span>
      }
    </div>
    @if (stale(); as theirs) {
      <app-conflict-card heading="The position changed while you edited" [traceId]="staleTraceId()">
        <p>{{ setBy(theirs) }} set {{ label(theirs) }} at {{ theirs.setAt | date: 'yyyy-MM-dd HH:mm' }} while you edited.</p>
        <button cardActions mat-stroked-button type="button" [disabled]="saving()" (click)="useTheirs(theirs)">Use theirs</button>
        <button cardActions mat-flat-button type="button" [disabled]="saving()" (click)="saveOver(theirs)">
          Save mine over it
        </button>
      </app-conflict-card>
    }
    <app-problem-banner [messages]="errors()" [traceId]="traceId()" />
  `,
  styles: `
    .editor {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 8px;
    }
    mat-button-toggle-group {
      --mat-button-toggle-height: 36px;
    }
    select {
      height: 38px;
      padding-inline: 10px 4px;
      border: 1px solid var(--mat-sys-outline);
      border-radius: 8px;
      background: var(--mat-sys-surface);
      color: var(--mat-sys-on-surface);
      font: var(--mat-sys-body-medium);
    }
    select:disabled {
      color: var(--mat-sys-on-surface-variant);
      border-color: var(--mat-sys-outline-variant);
    }
    .reason {
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-label-medium);
    }
  `,
})
export class PositionEditor {
  readonly voteId = input.required<number>();
  readonly position = input.required<PositionDto | null>();
  readonly saved = output<PositionDto>();

  private readonly service = inject(PositionsService);
  private readonly currentUser = inject(CurrentUser);
  protected readonly canEdit = this.currentUser.isAdmin;
  protected readonly weights = [1, 2, 3, 4, 5];

  /** The input by revision, so a worklist reload with the same position keeps this row's edit. */
  private readonly seen = computed(() => this.position(), { equal: (a, b) => a?.revisionId === b?.revisionId });
  /** The position the row shows: its input, or theirs once "Use theirs" took it. */
  private readonly shown = linkedSignal(() => this.seen());
  protected readonly stance = linkedSignal<Stance | null>(() => this.shown()?.stance ?? null);
  protected readonly weight = linkedSignal<number | null>(() => this.shown()?.weight ?? null);
  /** The position someone else saved while this row was edited, after a stale save. */
  protected readonly stale = linkedSignal<PositionDto | null>(() => (this.seen(), null));
  protected readonly staleTraceId = signal<string | undefined>(undefined);
  protected readonly saving = signal(false);
  protected readonly errors = signal<string[]>([]);
  protected readonly traceId = signal<string | undefined>(undefined);

  private readonly changed = computed(
    () => this.stance() !== (this.shown()?.stance ?? null) || this.weight() !== (this.shown()?.weight ?? null),
  );
  protected readonly canSave = computed(
    () => this.canEdit() && !this.saving() && this.changed() && this.stance() !== null && this.weight() !== null,
  );

  protected toWeight(event: Event): number | null {
    const value = Number((event.target as HTMLSelectElement).value);
    return value >= 1 && value <= 5 ? value : null;
  }

  protected label(position: PositionDto): string {
    return positionLabel(position);
  }

  protected setBy(position: PositionDto): string {
    const users = this.currentUser.users;
    return setByName(users.hasValue() ? users.value() : [], position);
  }

  protected save(): void {
    this.send(this.shown()?.revisionId ?? null);
  }

  /** Drops the edit and shows the position someone else saved. */
  protected useTheirs(theirs: PositionDto): void {
    this.stale.set(null);
    this.shown.set(theirs);
    this.saved.emit(theirs);
  }

  /** Resends this edit, now with their revision as the version it replaces. */
  protected saveOver(theirs: PositionDto): void {
    this.send(theirs.revisionId);
  }

  private send(seenRevisionId: number | null): void {
    this.saving.set(true);
    this.errors.set([]);
    this.service
      .setPosition(this.voteId(), { stance: this.stance(), weight: this.weight(), seenRevisionId })
      .subscribe({
        next: (position) => {
          this.saving.set(false);
          this.stale.set(null);
          this.shown.set(position);
          this.saved.emit(position);
        },
        error: (error: unknown) => {
          this.saving.set(false);
          const problem = toProblem(error);
          if (isProblem<StalePositionProblem>(problem, ProblemType.stalePosition)) {
            this.stale.set(problem.current);
            this.staleTraceId.set(problem.traceId);
            return;
          }
          this.errors.set(problemToFieldErrors(problem, () => undefined).formErrors);
          this.traceId.set(problem?.traceId);
        },
      });
  }
}
