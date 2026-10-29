import { ChangeDetectionStrategy, Component } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-not-found-page',
  imports: [RouterLink, MatButtonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="surface-card empty">
      <p class="code" aria-hidden="true">404</p>
      <h1>Page not found</h1>
      <p class="muted">The link may be mistyped, or the page has moved.</p>
      <a mat-flat-button routerLink="/review">Go to Review</a>
    </section>
  `,
  styles: `
    .empty {
      text-align: center;
      padding-block: 48px;
    }
    .code {
      font: var(--mat-sys-display-medium);
      color: var(--mat-sys-primary);
      margin: 0;
    }
    h1 {
      font: var(--mat-sys-headline-small);
    }
  `,
})
export class NotFoundPage {}
