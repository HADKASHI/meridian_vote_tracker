import { ChangeDetectionStrategy, Component, computed, inject, input, linkedSignal, output, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { PositionsService } from '../../api/positions/positions.service';
import { PositionDto, Stance } from '../../api/model';
import { CurrentUser } from '../../core/identity/current-user';
import { toProblem } from '../../shared/problem/problem';
import { ProblemBanner } from '../../shared/problem/problem-banner';
import { problemToFieldErrors } from '../../shared/problem/to-field-errors';

/**
 * One vote's stance and weight, edited inline. The save carries the revision the row
 * showed, so a position someone else saved meanwhile is never overwritten unseen.
 * Analysts see the controls disabled with the reason.
 */
@Component({
  selector: 'app-position-editor',
  imports: [MatButtonModule, MatButtonToggleModule, ProblemBanner],
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
        <option value="" disabled>Weight</option>
        @for (w of weights; track w) {
          <option [value]="w">{{ w }}</option>
        }
      </select>
      <button mat-stroked-button type="button" data-testid="save" [disabled]="!canSave()" (click)="save()">Save</button>
      @if (!canEdit()) {
        <span class="reason">Admin only</span>
      }
    </div>
    <app-problem-banner [messages]="errors()" [traceId]="traceId()" />
  `,
  styles: `
    .editor {
      display: flex;
      align-items: center;
      gap: 8px;
    }
    .reason {
      opacity: 0.7;
      font-size: 0.9em;
    }
  `,
})
export class PositionEditor {
  readonly voteId = input.required<number>();
  readonly position = input.required<PositionDto | null>();
  readonly saved = output<PositionDto>();

  private readonly service = inject(PositionsService);
  protected readonly canEdit = inject(CurrentUser).isAdmin;
  protected readonly weights = [1, 2, 3, 4, 5];

  protected readonly stance = linkedSignal<Stance | null>(() => this.position()?.stance ?? null);
  protected readonly weight = linkedSignal<number | null>(() => this.position()?.weight ?? null);
  protected readonly saving = signal(false);
  protected readonly errors = signal<string[]>([]);
  protected readonly traceId = signal<string | undefined>(undefined);

  private readonly changed = computed(
    () => this.stance() !== (this.position()?.stance ?? null) || this.weight() !== (this.position()?.weight ?? null),
  );
  protected readonly canSave = computed(
    () => this.canEdit() && !this.saving() && this.changed() && this.stance() !== null && this.weight() !== null,
  );

  protected toWeight(event: Event): number | null {
    const value = Number((event.target as HTMLSelectElement).value);
    return value >= 1 && value <= 5 ? value : null;
  }

  protected save(): void {
    this.saving.set(true);
    this.errors.set([]);
    this.service
      .setPosition(this.voteId(), {
        stance: this.stance(),
        weight: this.weight(),
        seenRevisionId: this.position()?.revisionId ?? null,
      })
      .subscribe({
        next: (position) => {
          this.saving.set(false);
          this.saved.emit(position);
        },
        error: (error: unknown) => {
          this.saving.set(false);
          const problem = toProblem(error);
          this.errors.set(problemToFieldErrors(problem, () => undefined).formErrors);
          this.traceId.set(problem?.traceId);
        },
      });
  }
}
