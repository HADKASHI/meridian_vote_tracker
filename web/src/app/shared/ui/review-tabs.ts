import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ReviewCounts } from './review-counts';

/** The Review inbox's two lists, with how many items wait in each. The page that shows it says which one is open. */
@Component({
  selector: 'app-review-tabs',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <nav class="tabs" aria-label="Review inbox">
      <a routerLink="/review" [attr.aria-current]="open() === 'conflicts' ? 'page' : null">
        Conflicts <span class="count" data-testid="conflicts-count">{{ counts.conflicts() }}</span>
      </a>
      <a routerLink="/review/extractions" [attr.aria-current]="open() === 'extractions' ? 'page' : null">
        Extractions <span class="count" data-testid="extractions-count">{{ counts.extractions() }}</span>
      </a>
    </nav>
  `,
  styles: `
    .tabs {
      display: flex;
      gap: 4px;
      margin-block-end: 12px;
      border-block-end: 1px solid var(--mat-sys-outline-variant);
    }
    a {
      padding: 8px 16px;
      color: var(--mat-sys-on-surface-variant);
      text-decoration: none;
      font: var(--mat-sys-label-large);
      border-block-end: 3px solid transparent;
    }
    a[aria-current='page'] {
      color: var(--mat-sys-primary);
      border-block-end-color: var(--mat-sys-primary);
    }
    .count {
      display: inline-block;
      min-width: 1.6em;
      margin-inline-start: 4px;
      padding: 0 6px;
      border-radius: 10px;
      text-align: center;
      background: var(--mat-sys-surface-container-high);
      color: var(--mat-sys-on-surface);
    }
  `,
})
export class ReviewTabs {
  readonly open = input.required<'conflicts' | 'extractions'>();
  protected readonly counts = inject(ReviewCounts);
}
