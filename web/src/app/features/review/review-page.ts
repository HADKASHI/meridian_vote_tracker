import { ChangeDetectionStrategy, Component, effect, inject, input } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { ConflictFilter, ConflictListItemDto } from '../../api/model';
import { changeLabel, decisionLabel, subjectLabel } from './conflict-values';
import { ReviewInbox, toFilter } from './review-inbox';

/** The Review inbox: the conflict list beside the decision pane (the child route). */
@Component({
  selector: 'app-review-page',
  imports: [MatButtonModule, RouterLink, RouterLinkActive, RouterOutlet],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>Review</h1>
    <nav class="filters" aria-label="Which conflicts">
      @for (f of filters; track f.value) {
        <a mat-button [routerLink]="[]" [queryParams]="{ state: f.value }" [class.active]="inbox.filter() === f.value">{{ f.label }}</a>
      }
    </nav>
    <div class="split">
      <section class="list" aria-label="Conflicts">
        @if (inbox.list.hasValue()) {
          @if (inbox.items().length === 0) {
            <p>No conflicts {{ inbox.filter() === 'pending' ? 'waiting for review' : 'here' }}.</p>
          }
          <ul>
            @for (item of inbox.items(); track item.id) {
              <li data-testid="inbox-item">
                <a [routerLink]="[item.id]" [queryParams]="{ state: inbox.filter() }" routerLinkActive="selected">
                  <span class="vote">{{ item.vote.assemblyVoteId }}</span>
                  <span class="subject">{{ subject(item) }}</span>
                  <span class="change">{{ change(item) }}</span>
                  <span class="import">Import #{{ item.importId }}</span>
                  @if (item.decision) {
                    <span class="decision">{{ decision(item.decision) }}</span>
                  }
                </a>
              </li>
            }
          </ul>
          @if (inbox.total() > inbox.items().length) {
            <p class="more">Showing {{ inbox.items().length }} of {{ inbox.total() }}; decide these to see the rest.</p>
          }
        } @else if (inbox.list.error()) {
          <p>The conflicts could not be loaded.</p>
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
      gap: 4px;
    }
    .filters .active {
      font-weight: 600;
      text-decoration: underline;
    }
    .split {
      display: grid;
      grid-template-columns: minmax(280px, 2fr) 3fr;
      gap: 24px;
      align-items: start;
    }
    @media (max-width: 800px) {
      .split {
        grid-template-columns: 1fr;
      }
    }
    ul {
      list-style: none;
      margin: 0;
      padding: 0;
    }
    li a {
      display: grid;
      grid-template-columns: auto 1fr;
      gap: 2px 12px;
      padding: 8px 12px;
      border-block-end: 1px solid var(--mat-sys-outline-variant);
      color: inherit;
      text-decoration: none;
    }
    li a.selected {
      background: var(--mat-sys-secondary-container);
    }
    .vote {
      font-weight: 600;
    }
    .change {
      grid-column: 1 / -1;
      font-family: monospace;
    }
    .import,
    .decision,
    .more,
    .hint {
      opacity: 0.75;
      font-size: 0.9em;
    }
  `,
})
export class ReviewPage {
  /** The `?state=` query param: pending (the default), decided or all. */
  readonly state = input<string>();
  protected readonly inbox = inject(ReviewInbox);
  protected readonly filters = [
    { value: ConflictFilter.pending, label: 'Pending' },
    { value: ConflictFilter.decided, label: 'Decided' },
    { value: ConflictFilter.all, label: 'All' },
  ];
  protected readonly subject = subjectLabel;
  protected readonly decision = decisionLabel;

  constructor() {
    effect(() => this.inbox.filter.set(toFilter(this.state())));
  }

  protected change(item: ConflictListItemDto): string {
    return changeLabel(item.subject, item.current, item.incoming);
  }
}
