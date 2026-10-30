import { ChangeDetectionStrategy, Component, inject, Injector, output, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { Router } from '@angular/router';
import { ExtractionsService } from '../../api/extractions/extractions.service';
import { Problem, toProblem } from '../problem/problem';
import { ReviewCounts } from './review-counts';

/**
 * The button that uploads a scanned voting sheet (a PDF), wherever a page offers it. A new sheet opens in the Review
 * inbox; a page that shows the inbox's list hears about it through `uploaded` and one that shows a refusal through `failed`.
 */
@Component({
  selector: 'app-voting-sheet-upload',
  imports: [MatButtonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <input #picker type="file" accept=".pdf,application/pdf" hidden (change)="upload(picker)" />
    <button mat-flat-button type="button" data-testid="upload-sheet" [disabled]="uploading()" (click)="picker.click()">
      {{ uploading() ? 'Uploading…' : 'Upload a voting sheet' }}
    </button>
  `,
})
export class VotingSheetUpload {
  readonly uploaded = output<void>();
  readonly failed = output<Problem | null>();
  protected readonly uploading = signal(false);
  private readonly service = inject(ExtractionsService);
  private readonly router = inject(Router);
  // Asked for only once a sheet is uploaded: building the counts reads the Review inbox, which most pages that offer this button don't show.
  private readonly injector = inject(Injector);

  protected upload(picker: HTMLInputElement): void {
    const file = picker.files?.[0];
    if (!file) return;
    this.uploading.set(true);
    this.service.uploadExtraction({ file }).subscribe({
      next: (created) => {
        this.uploading.set(false);
        picker.value = '';
        this.uploaded.emit();
        this.injector.get(ReviewCounts).reload();
        void this.router.navigate(['/review/extractions', created.extractionId], { queryParams: { state: 'open' } });
      },
      error: (error: unknown) => {
        this.uploading.set(false);
        picker.value = '';
        this.failed.emit(toProblem(error));
      },
    });
  }
}
