import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { ConflictFilter, ConflictListItemDto } from '../../api/model';
import { LoadState } from '../../shared/ui/load-state';
import { PageHeader } from '../../shared/ui/page-header';
import { ReviewTabs } from '../../shared/ui/review-tabs';
import { changeLabel, decisionLabel, subjectLabel } from './conflict-values';
import { ReviewInbox, toFilter, toScope } from './review-inbox';

/** The Review inbox: the conflict list beside the decision pane (the child route). */
@Component({
  selector: 'app-review-page',
  imports: [RouterLink, RouterLinkActive, RouterOutlet, PageHeader, LoadState, ReviewTabs],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-page-header heading="Review">
      <p pageLead>A decision takes the incoming value or keeps the current one; one decision per conflict.</p>
    </app-page-header>
    <app-review-tabs open="conflicts" />
    <nav class="filters" aria-label="Which conflicts">
      @for (f of filters; track f.value) {
        <a class="filter" [routerLink]="[]" [queryParams]="{ state: f.value }" queryParamsHandling="merge"
          [attr.aria-current]="inbox.filter() === f.value ? 'true' : null">{{ f.label }}</a>
      }
      @if (inbox.narrowed()) {
        <span class="narrowed">
          Only some conflicts are shown.
          <a [routerLink]="[]" [queryParams]="{ voteIds: null, importId: null }" queryParamsHandling="merge">Show all</a>
        </span>
      }
    </nav>
    <div class="split">
      <section class="list" aria-label="Conflicts">
        @if (inbox.list.hasValue()) {
          @if (inbox.items().length === 0) {
            <p class="empty">No conflicts {{ inbox.filter() === 'pending' ? 'waiting for review' : 'here' }}.</p>
          }
          <ul>
            @for (item of inbox.items(); track item.id) {
              <li data-testid="inbox-item">
                <a [routerLink]="[item.id]" [queryParams]="inbox.queryParams()" routerLinkActive="selected"
                   [ariaCurrentWhenActive]="true">
                  <span class="vote">{{ item.vote.assemblyVoteId }}</span>
                  <span class="subject chip" [attr.data-tone]="item.subject === 'details' ? 'tertiary' : 'primary'">{{ subject(item) }}</span>
                  <span class="change">{{ change(item) }}</span>
                  <span class="import">Import #{{ item.importId }}</span>
                  @if (item.decision) {
                    <span class="decision chip" data-tone="secondary">{{ decision(item.decision) }}</span>
                  }
                </a>
              </li>
            }
          </ul>
          @if (inbox.total() > inbox.items().length) {
            <p class="more">Showing {{ inbox.items().length }} of {{ inbox.total() }}; decide these to see the rest.</p>
          }
        } @else if (inbox.list.error()) {
          <app-load-state kind="error" message="The conflicts could not be loaded." />
        } @else {
          <app-load-state kind="loading" message="Loading conflicts" />
        }
      </section>
      <section class="pane">
        <router-outlet #outlet="outlet" />
        @if (!outlet.isActivated) {
          <p class="hint">Choose a conflict to see both values and their evidence.</p>
        }
      </section>
    </div>
  `,
  styles: `
    .filters {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
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
    .narrowed {
      color: var(--mat-sys-on-surface-variant);
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
      display: grid;
      grid-template-columns: auto minmax(0, 1fr) auto;
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
    /* The open conflict: a bar and a fill, not colour alone. */
    li a.selected {
      background: var(--mat-sys-secondary-container);
      box-shadow: inset 4px 0 0 var(--mat-sys-primary);
    }
    .vote {
      font-family: var(--app-mono);
      font-weight: 600;
    }
    .subject {
      justify-self: start;
    }
    .import {
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-label-medium);
      grid-row: 1;
      grid-column: 3;
      justify-self: end;
    }
    .change {
      grid-column: 1 / -1;
      font-family: var(--app-mono);
      overflow-wrap: anywhere;
    }
    .decision {
      grid-column: 1 / -1;
      justify-self: start;
    }
    .empty,
    .more,
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
export class ReviewPage {
  /** The `?state=` query param: pending (the default), decided or all. */
  readonly state = input<string>();
  /** `?voteIds=` (repeated) and `?importId=`, as the snapshot preview and the import report link here. */
  readonly voteIds = input<string | string[]>();
  readonly importId = input<string>();
  protected readonly inbox = inject(ReviewInbox);
  protected readonly filters = [
    { value: ConflictFilter.pending, label: 'Pending' },
    { value: ConflictFilter.decided, label: 'Decided' },
    { value: ConflictFilter.all, label: 'All' },
  ];
  protected readonly subject = subjectLabel;
  protected readonly decision = decisionLabel;

  constructor() {
    this.inbox.bind(
      computed(() => toFilter(this.state())),
      computed(() => toScope(this.voteIds(), this.importId())),
    );
  }

  protected change(item: ConflictListItemDto): string {
    return changeLabel(item.subject, item.current, item.incoming);
  }
}
