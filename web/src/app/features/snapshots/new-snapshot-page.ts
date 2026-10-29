import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatDialog, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { Router } from '@angular/router';
import { NoPositionProblem, SnapshotPreviewDto } from '../../api/model';
import { SnapshotsService } from '../../api/snapshots/snapshots.service';
import { ProblemBanner } from '../../shared/problem/problem-banner';
import { PageHeader } from '../../shared/ui/page-header';
import { isProblem, ProblemType, toProblem } from '../../shared/problem/problem';
import { problemToFieldErrors } from '../../shared/problem/to-field-errors';
import { PreviewDialog, PreviewDialogData } from './preview-dialog';
import { describeSelection, SelectionForm, SelectionMode, toSelection } from './selection';

type Field = 'name' | 'note' | 'from' | 'to' | 'sessions' | 'voteIds';

/** SNP-01: name a snapshot, choose its votes, preview them, then calculate. */
@Component({
  selector: 'app-new-snapshot-page',
  imports: [MatButtonModule, MatButtonToggleModule, MatFormFieldModule, MatInputModule, ProblemBanner, PageHeader],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header heading="New snapshot" [back]="{ label: 'Snapshots', link: '/snapshots' }">
      <p pageLead>Choose votes, preview, then calculate. Calculation runs as a job.</p>
    </app-page-header>
    <form class="surface-card form" (submit)="$event.preventDefault(); preview()">
      <mat-form-field>
        <mat-label>Name</mat-label>
        <input matInput name="name" [value]="name()" (input)="name.set(value($event))" />
        @if (error('name'); as message) {
          <mat-hint class="field-error" data-testid="name-error">{{ message }}</mat-hint>
        }
      </mat-form-field>

      <span class="label" id="mode-label">Select votes by</span>
      <mat-button-toggle-group
        aria-labelledby="mode-label"
        [value]="form().mode"
        (change)="setMode($event.value)"
        data-testid="selection-mode"
      >
        <mat-button-toggle value="range">Date range</mat-button-toggle>
        <mat-button-toggle value="sessions">Sessions</mat-button-toggle>
        <mat-button-toggle value="list">List</mat-button-toggle>
      </mat-button-toggle-group>

      @switch (form().mode) {
        @case ('range') {
          <div class="row">
            <mat-form-field>
              <mat-label>From</mat-label>
              <input matInput type="date" name="from" [value]="form().from" (input)="patch({ from: value($event) })" />
              @if (error('from'); as message) {
                <mat-hint class="field-error">{{ message }}</mat-hint>
              }
            </mat-form-field>
            <mat-form-field>
              <mat-label>To</mat-label>
              <input matInput type="date" name="to" [value]="form().to" (input)="patch({ to: value($event) })" />
              @if (error('to'); as message) {
                <mat-hint class="field-error">{{ message }}</mat-hint>
              }
            </mat-form-field>
          </div>
        }
        @case ('sessions') {
          <mat-form-field>
            <mat-label>Sessions</mat-label>
            <input matInput name="sessions" [value]="form().sessions" (input)="patch({ sessions: value($event) })" />
            <mat-hint>{{ error('sessions') ?? 'Session numbers, separated by commas' }}</mat-hint>
          </mat-form-field>
        }
        @case ('list') {
          <mat-form-field>
            <mat-label>Votes</mat-label>
            <input matInput name="voteIds" [value]="form().voteIds" (input)="patch({ voteIds: value($event) })" />
            <mat-hint>{{ error('voteIds') ?? 'Vote ids from the vote pages, separated by commas' }}</mat-hint>
          </mat-form-field>
        }
      }

      @if (form().removed.length > 0) {
        <p class="notice removed" data-testid="removed-votes">
          Removed: {{ removedLabel() }}
          <button mat-button type="button" (click)="patch({ removed: [] })">Put them back</button>
        </p>
      }

      <mat-form-field>
        <mat-label>Note (optional)</mat-label>
        <textarea matInput name="note" rows="3" [value]="note()" (input)="note.set(value($event))"></textarea>
        @if (error('note'); as message) {
          <mat-hint class="field-error">{{ message }}</mat-hint>
        }
      </mat-form-field>

      <app-problem-banner [messages]="formErrors()" [traceId]="traceId()" />
      <div class="submit">
        <button mat-flat-button type="submit" [disabled]="busy()">Preview…</button>
      </div>
    </form>
  `,
  styles: `
    .form {
      display: flex;
      flex-direction: column;
      gap: 8px;
      max-width: 640px;
      padding: 20px;
    }
    .row {
      display: flex;
      flex-wrap: wrap;
      gap: 0 16px;
    }
    .row mat-form-field {
      flex: 1 1 200px;
    }
    .label {
      font: var(--mat-sys-label-large);
      color: var(--mat-sys-on-surface-variant);
    }
    mat-button-toggle-group {
      align-self: flex-start;
      margin-block-end: 12px;
      max-width: 100%;
      overflow-x: auto;
    }
    .removed {
      margin: 0;
    }
    .submit {
      margin-block-start: 8px;
    }
    .field-error {
      color: var(--mat-sys-error);
    }
  `,
})
export class NewSnapshotPage {
  private readonly snapshots = inject(SnapshotsService);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);

  protected readonly name = signal('');
  protected readonly note = signal('');
  protected readonly form = signal<SelectionForm>({
    mode: 'range', from: '', to: '', sessions: '', voteIds: '', removed: [],
  });
  protected readonly busy = signal(false);
  protected readonly lastPreview = signal<SnapshotPreviewDto | null>(null);
  protected readonly fieldErrors = signal<Partial<Record<Field, string>>>({});
  protected readonly formErrors = signal<string[]>([]);
  protected readonly traceId = signal<string | undefined>(undefined);
  protected readonly removedLabel = computed(() => this.form().removed.map((v) => v.assemblyVoteId).join(', '));

  private dialogRef: MatDialogRef<PreviewDialog> | null = null;

  protected value(event: Event): string {
    return (event.target as HTMLInputElement | HTMLTextAreaElement).value;
  }

  protected error(field: Field): string | undefined {
    return this.fieldErrors()[field];
  }

  protected setMode(mode: SelectionMode): void {
    this.patch({ mode, removed: [] });
  }

  protected patch(change: Partial<SelectionForm>): void {
    this.form.update((form) => ({ ...form, ...change }));
    this.lastPreview.set(null);
  }

  /** Previews the selection and opens the dialog, or refreshes it when it is already open. */
  protected preview(): void {
    this.busy.set(true);
    this.snapshots.previewSnapshot(toSelection(this.form())).subscribe({
      next: (preview) => {
        this.busy.set(false);
        this.clearErrors();
        this.lastPreview.set(preview);
        this.openDialog();
      },
      error: (error: unknown) => {
        this.busy.set(false);
        this.dialogRef?.close();
        this.showProblem(error, 'selection');
      },
    });
  }

  /** "Remove these votes": the selection becomes the rest, recorded as the criteria minus these votes. */
  protected removeBlocked(): void {
    const blocked = this.lastPreview()?.noPosition ?? [];
    this.form.update((form) => ({ ...form, removed: [...form.removed, ...blocked] }));
    this.preview();
  }

  /** Creates the snapshot; its job calculates it, and its page follows the job. */
  protected calculate(): void {
    this.busy.set(true);
    const request = { name: this.name(), note: this.note() || null, selection: toSelection(this.form()) };
    this.snapshots.createSnapshot(request).subscribe({
      next: (created) => {
        this.busy.set(false);
        this.dialogRef?.close();
        void this.router.navigate(['/snapshots', created.snapshotId]);
      },
      error: (error: unknown) => {
        const problem = toProblem(error);
        this.busy.set(false);
        // A vote lost its place since the preview: show the fresh blocking list in the dialog.
        if (isProblem<NoPositionProblem>(problem, ProblemType.noPosition) && this.lastPreview()) {
          this.lastPreview.update((preview) => preview && { ...preview, noPosition: problem.votes });
          return;
        }
        this.dialogRef?.close();
        this.showProblem(error, 'selection');
      },
    });
  }

  private openDialog(): void {
    if (this.dialogRef) return;
    const data: PreviewDialogData = {
      name: this.name.asReadonly(),
      label: computed(() => describeSelection(this.form())),
      preview: this.lastPreview.asReadonly(),
      busy: this.busy.asReadonly(),
      removeBlocked: () => this.removeBlocked(),
      checkAgain: () => this.preview(),
      calculate: () => this.calculate(),
    };
    this.dialogRef = this.dialog.open(PreviewDialog, { data, width: '680px' });
    this.dialogRef.afterClosed().subscribe(() => (this.dialogRef = null));
  }

  private clearErrors(): void {
    this.fieldErrors.set({});
    this.formErrors.set([]);
    this.traceId.set(undefined);
  }

  /** Puts a problem's messages on their fields; `prefix` is the JSON path of the selection in the request. */
  protected showProblem(error: unknown, prefix: string | null): void {
    const problem = toProblem(error);
    const fields: Field[] = ['name', 'note', 'from', 'to', 'sessions', 'voteIds'];
    const mapped = problemToFieldErrors<Field>(problem, (path) => {
      const local = prefix && path.startsWith(`${prefix}.`) ? path.slice(prefix.length + 1) : path;
      const field = local.replace(/\[\d+\]$/, '');
      return fields.find((f) => f === field);
    });
    this.fieldErrors.set(Object.fromEntries(mapped.fieldErrors.map((e) => [e.field, e.message])));
    this.formErrors.set(problem ? mapped.formErrors : ['The server could not be reached.']);
    this.traceId.set(problem?.traceId);
  }
}
