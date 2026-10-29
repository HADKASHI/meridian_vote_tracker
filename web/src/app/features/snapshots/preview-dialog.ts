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
          <section class="notice" data-tone="error" role="alert" data-testid="preview-blocked">
            <h3>
              {{ preview.noPosition.length }} {{ preview.noPosition.length === 1 ? 'vote has' : 'votes have' }} no position,
              so this snapshot can't be calculated
            </h3>
            <ul class="votes">
              @for (vote of preview.noPosition; track vote.voteId) {
                <li><a [routerLink]="['/votes', vote.voteId]">{{ vote.assemblyVoteId }}</a></li>
              }
            </ul>
            <p class="hint">
              Ask the admin to set positions (<a routerLink="/positions">Positions</a>), or remove them from this
              selection. Removal is recorded with the selection.
            </p>
            <div class="actions">
              <button mat-stroked-button type="button" [disabled]="data.busy()" (click)="data.removeBlocked()">
                Remove these votes
              </button>
              <button mat-stroked-button type="button" [disabled]="data.busy()" (click)="data.checkAgain()">
                Check again
              </button>
            </div>
          </section>
        } @else {
          <p class="notice" data-testid="preview-ready">Every selected vote has a position.</p>
        }
        @if (preview.warnings.length > 0) {
          <section class="notice" data-tone="warning" data-testid="preview-warnings">
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
      color: var(--mat-sys-on-surface-variant);
      margin-block: 0 12px;
    }
    .notice {
      margin-block: 0 12px;
    }
    h3 {
      margin-block: 0 4px;
      font: var(--mat-sys-title-small);
    }
    .votes {
      columns: 2 9rem;
      font-family: var(--app-mono);
    }
    .actions {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
    }
    .hint {
      font: var(--mat-sys-body-small);
    }
    .end {
      text-align: end;
      padding-inline: 24px;
      color: var(--mat-sys-on-surface-variant);
      margin-block: 0 16px;
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
