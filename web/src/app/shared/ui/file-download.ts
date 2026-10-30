import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { Problem, toProblem } from '../problem/problem';
import { ProblemBanner } from '../problem/problem-banner';

/**
 * The button that downloads the original file of an import or a voting sheet. It goes through the API (and so the
 * identity header and the role checks), because no link to the file works without them. A refusal is shown beside the button.
 */
@Component({
  selector: 'app-file-download',
  imports: [MatButtonModule, ProblemBanner],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button mat-stroked-button type="button" data-testid="download" [disabled]="busy()" [attr.aria-label]="'Download ' + fileName()"
            (click)="download()">
      {{ busy() ? 'Downloading…' : 'Download' }}
    </button>
    @if (problem(); as p) {
      <app-problem-banner [messages]="[p.detail ?? p.title]" [traceId]="p.traceId" />
    }
  `,
})
export class FileDownload {
  /** The endpoint that streams the file, e.g. `/api/imports/7/file`. */
  readonly url = input.required<string>();
  /** The name the browser saves it under: the one it was uploaded with. */
  readonly fileName = input.required<string>();
  protected readonly busy = signal(false);
  protected readonly problem = signal<Problem | null>(null);
  private readonly http = inject(HttpClient);

  protected download(): void {
    this.busy.set(true);
    this.problem.set(null);
    this.http.get(this.url(), { responseType: 'blob' }).subscribe({
      next: (file) => {
        this.busy.set(false);
        const link = document.createElement('a');
        link.href = URL.createObjectURL(file);
        link.download = this.fileName();
        link.click();
        URL.revokeObjectURL(link.href);
      },
      error: (error: unknown) => void this.fail(error),
    });
  }

  /** A blob request delivers even a problem body as a blob, so it is read back into the problem it holds. */
  private async fail(error: unknown): Promise<void> {
    let problem: Problem | null = null;
    if (error instanceof HttpErrorResponse && error.error instanceof Blob) {
      try {
        problem = toProblem(JSON.parse(await error.error.text()));
      } catch {
        // Not a problem body: the generic notice below says it.
      }
    } else {
      problem = toProblem(error);
    }
    this.busy.set(false);
    this.problem.set(problem ?? { type: 'unexpected', title: 'The file could not be downloaded.', status: 0 });
  }
}
