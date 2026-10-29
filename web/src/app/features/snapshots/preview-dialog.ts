import { ChangeDetectionStrategy, Component, inject, Signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { RouterLink } from '@angular/router';
import { SnapshotPreviewDto } from '../../api/model';

export interface PreviewDialogData {
  name: Signal<string>;
  label: Signal<string>;
  preview: Signal<SnapshotPreviewDto | null>;
  busy: Signal<boolean>;
  removeBlocked: () => void;
  checkAgain: () => void;
  calculate: () => void;
}

/** The preview before calculating: votes without a position block; warnings never do. */
@Component({
  selector: 'app-preview-dialog',
  imports: [MatDialogModule, MatButtonModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>Preview: {{ data.name() || 'unnamed snapshot' }}</h2>
    <mat-dialog-content>
      @if (data.preview(); as preview) {
        <p class="summary" data-testid="preview-summary">
          {{ data.label() }} · <strong>{{ preview.voteCount }} {{ preview.voteCount === 1 ? 'vote' : 'votes' }}</strong>
        </p>
        @if (preview.noPosition.length > 0) {
          <section class="blocked" role="alert" data-testid="preview-blocked">
            <h3>
              {{ preview.noPosition.length }} {{ preview.noPosition.length === 1 ? 'vote has' : 'votes have' }} no position,
              so this snapshot can't be calculated
            </h3>
            <ul>
              @for (vote of preview.noPosition; track vote.voteId) {
                <li><a [routerLink]="['/votes', vote.voteId]">{{ vote.assemblyVoteId }}</a></li>
              }
            </ul>
            <p class="hint">
              Ask the admin to set positions (<a routerLink="/positions">Positions</a>), or remove them from this
              selection. Removal is recorded with the selection.
            </p>
            <button mat-stroked-button type="button" [disabled]="data.busy()" (click)="data.removeBlocked()">
              Remove these votes
            </button>
            <button mat-stroked-button type="button" [disabled]="data.busy()" (click)="data.checkAgain()">
              Check again
            </button>
          </section>
        } @else {
          <p class="ready" data-testid="preview-ready">Every selected vote has a position.</p>
        }
        @if (preview.warnings.length > 0) {
          <section class="warnings" data-testid="preview-warnings">
            <h3>Warnings (kept on the snapshot, never block)</h3>
            <ul>
              @for (warning of preview.warnings; track $index) {
                <li>{{ warning.message }}</li>
              }
            </ul>
          </section>
        }
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-stroked-button type="button" mat-dialog-close>Back</button>
      <button
        mat-flat-button
        type="button"
        [disabled]="!canCalculate()"
        (click)="data.calculate()"
        data-testid="calculate"
      >
        Calculate
      </button>
    </mat-dialog-actions>
    @if (blocked()) {
      <p class="hint end">Calculate is disabled while votes without a position are selected.</p>
    }
  `,
  styles: `
    .summary {
      opacity: 0.8;
    }
    .blocked,
    .warnings,
    .ready {
      border-inline-start: 4px solid;
      border-radius: 8px;
      padding: 8px 16px;
      margin-block: 8px;
    }
    .blocked {
      border-color: var(--mat-sys-error);
      background: var(--mat-sys-error-container);
      color: var(--mat-sys-on-error-container);
    }
    .blocked button {
      margin-inline-end: 8px;
    }
    .warnings {
      border-color: var(--mat-sys-tertiary);
      background: var(--mat-sys-tertiary-container);
      color: var(--mat-sys-on-tertiary-container);
    }
    .ready {
      border-color: var(--mat-sys-primary);
    }
    h3 {
      margin-block: 4px;
      font-size: 1em;
    }
    .hint {
      font-size: 0.85em;
      opacity: 0.8;
    }
    .end {
      text-align: end;
      padding-inline: 24px;
    }
  `,
})
export class PreviewDialog {
  protected readonly data = inject<PreviewDialogData>(MAT_DIALOG_DATA);

  protected blocked(): boolean {
    return (this.data.preview()?.noPosition.length ?? 0) > 0;
  }

  protected canCalculate(): boolean {
    return this.data.preview() !== null && !this.blocked() && !this.data.busy();
  }
}
