import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { pollWhile } from '../../shared/http/poll';
import { Problem } from '../../shared/problem/problem';
import { ProblemBanner } from '../../shared/problem/problem-banner';
import { LoadState } from '../../shared/ui/load-state';
import { PageHeader } from '../../shared/ui/page-header';
import { ReviewTabs } from '../../shared/ui/review-tabs';
import { VotingSheetUpload } from '../../shared/ui/voting-sheet-upload';
import { READING_POLL_MS, statusLabel, statusTone } from './extraction-labels';
import { ExtractionsInbox, toFilter } from './extractions-inbox';

/** The Extractions tab of the Review inbox: scanned voting sheets, the upload that starts one, and the open sheet beside the list. */
@Component({
  selector: 'app-extractions-page',
  imports: [RouterLink, RouterLinkActive, RouterOutlet, PageHeader, LoadState, ProblemBanner, ReviewTabs, VotingSheetUpload],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header heading="Review">
      <p pageLead>
        A scanned voting sheet is read in the background and checked against the totals it prints. Nothing counts until you approve it.
      </p>
      <div pageActions>
        <app-voting-sheet-upload (uploaded)="problem.set(null); inbox.list.reload()" (failed)="problem.set($event)" />
      </div>
    </app-page-header>
    <app-review-tabs open="extractions" />
    @if (problem(); as p) {
      <app-problem-banner [messages]="[p.detail ?? p.title]" [traceId]="p.traceId" />
    }
    <nav class="filters" aria-label="Which extractions">
      @for (f of filters; track f.value) {
        <a class="filter" [routerLink]="[]" [queryParams]="{ state: f.value }" queryParamsHandling="merge"
          [attr.aria-current]="inbox.filter() === f.value ? 'true' : null">{{ f.label }}</a>
      }
    </nav>
    <div class="split">
      <section class="list" aria-label="Extractions">
        @if (inbox.list.hasValue()) {
          @if (inbox.items().length === 0) {
            <p class="empty">No sheets {{ inbox.filter() === 'open' ? 'waiting for you' : 'here' }}.</p>
          }
          <ul>
            @for (item of inbox.items(); track item.id) {
              <li data-testid="extraction-item">
                <a [routerLink]="[item.id]" [queryParams]="{ state: inbox.filter() }" routerLinkActive="selected" [ariaCurrentWhenActive]="true">
                  <span class="file">{{ item.fileName }}</span>
                  <span class="chip" [attr.data-tone]="tone(item.status)">{{ label(item.status) }}</span>
                  <span class="vote">{{ item.voteId ?? 'not read yet' }}</span>
                  @if (item.blockingIssues > 0) {
                    <span class="issues blocking">{{ item.blockingIssues }} blocking</span>
                  }
                  @if (item.warnings > 0) {
                    <span class="issues">{{ item.warnings }} {{ item.warnings === 1 ? 'warning' : 'warnings' }}</span>
                  }
                  <span class="by">{{ item.uploadedBy }}</span>
                </a>
              </li>
            }
          </ul>
        } @else if (inbox.list.error()) {
          <app-load-state kind="error" message="The extractions could not be loaded." />
        } @else {
          <app-load-state kind="loading" message="Loading extractions" />
        }
      </section>
      <section class="pane">
        <router-outlet #outlet="outlet" />
        @if (!outlet.isActivated) {
          <p class="hint">Choose a sheet to check what was read from it.</p>
        }
      </section>
    </div>
  `,
  styles: `
    .filters {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
      margin-block-end: 16px;
    }
    .filter {
      padding: 6px 14px;
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: 8px;
      background: var(--mat-sys-surface);
      color: var(--mat-sys-on-surface);
      text-decoration: none;
      font: var(--mat-sys-label-large);
    }
    .filter[aria-current='true'] {
      background: var(--mat-sys-secondary-container);
      color: var(--mat-sys-on-secondary-container);
      border-color: transparent;
    }
    .filter[aria-current='true']::before {
      content: '✓ ';
    }
    .split {
      display: grid;
      grid-template-columns: minmax(0, 2fr) minmax(0, 3fr);
      gap: 24px;
      align-items: start;
    }
    @media (max-width: 900px) {
      .split {
        grid-template-columns: minmax(0, 1fr);
      }
    }
    .list,
    .pane {
      background: var(--mat-sys-surface);
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: 12px;
      overflow: hidden;
    }
    .pane {
      padding: 16px 20px;
    }
    ul {
      list-style: none;
      margin: 0;
      padding: 0;
    }
    li a {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 4px 12px;
      padding: 12px 16px;
      border-block-end: 1px solid var(--mat-sys-outline-variant);
      color: inherit;
      text-decoration: none;
    }
    li:last-child a {
      border-block-end: none;
    }
    li a:hover {
      background: var(--mat-sys-surface-container);
    }
    li a.selected {
      background: var(--mat-sys-secondary-container);
      box-shadow: inset 4px 0 0 var(--mat-sys-primary);
    }
    .file {
      font-weight: 600;
      overflow-wrap: anywhere;
    }
    .vote {
      font-family: var(--app-mono);
    }
    .by,
    .issues {
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-label-medium);
    }
    .issues.blocking {
      color: var(--mat-sys-error);
      font-weight: 600;
    }
    .empty,
    .hint {
      color: var(--mat-sys-on-surface-variant);
      padding: 16px;
      margin: 0;
    }
    .hint {
      padding: 32px 0;
      text-align: center;
    }
  `,
})
export class ExtractionsPage {
  /** The `?state=` query param: open (the default), decided or all. */
  readonly state = input<string>();
  protected readonly inbox = inject(ExtractionsInbox);
  protected readonly problem = signal<Problem | null>(null);
  protected readonly filters = [
    { value: 'open', label: 'Open' },
    { value: 'decided', label: 'Decided' },
    { value: 'all', label: 'All' },
  ] as const;
  protected readonly label = statusLabel;
  protected readonly tone = statusTone;

  constructor() {
    // The route's query param is the filter; the list keeps asking while a sheet is being read.
    this.inbox.bind(computed(() => toFilter(this.state())));
    pollWhile(this.inbox.list, READING_POLL_MS, this.inbox.reading);
  }
}
