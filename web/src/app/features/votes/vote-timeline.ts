import { ChangeDetectionStrategy, Component, computed, input, linkedSignal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { RouterLink } from '@angular/router';
import { getVoteTimelineResource } from '../../api/votes/votes.resource';
import { TimelineEventDto } from '../../api/model';
import { Origin } from './origin';
import { describeEvent } from './timeline-labels';

/** HIS-03, HIS-04: the vote's whole story, newest first; "Older" loads the next page. */
@Component({
  selector: 'app-vote-timeline',
  imports: [DatePipe, MatButtonModule, RouterLink, Origin],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ol class="timeline" data-testid="timeline">
      @for (event of events(); track event.historyId) {
        <li [attr.data-kind]="event.kind">
          <div class="meta">{{ event.at | date: 'yyyy-MM-dd HH:mm:ss' }} · {{ event.actor }}</div>
          <div class="what">{{ describe(event) }}</div>
          @if (event.comment) {
            <div class="comment">“{{ event.comment }}”</div>
          }
          @if (event.origin) {
            <div class="meta">Origin: <app-origin [origin]="event.origin" /></div>
          }
          @if (event.snapshotId) {
            <a [routerLink]="['/snapshots', event.snapshotId]">Open the snapshot</a>
          }
        </li>
      } @empty {
        @if (!page.isLoading()) {
          <li class="empty">Nothing has happened to this vote yet.</li>
        }
      }
    </ol>
    @if (nextBefore(); as next) {
      <button
        mat-stroked-button
        type="button"
        (click)="before.set(next)"
        [disabled]="page.isLoading()"
      >
        Older
      </button>
    }
  `,
  styles: `
    .timeline {
      list-style: none;
      margin: 0 0 16px;
      padding: 0 0 0 20px;
      border-inline-start: 2px solid var(--mat-sys-outline-variant);
    }
    .timeline li {
      position: relative;
      padding: 12px 16px;
      margin-block-end: 12px;
      background: var(--mat-sys-surface);
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: 12px;
      overflow-wrap: anywhere;
    }
    /* A dot on the line for each event. */
    .timeline li::before {
      content: '';
      position: absolute;
      inset-inline-start: -28px;
      inset-block-start: 18px;
      width: 12px;
      height: 12px;
      border-radius: 50%;
      background: var(--mat-sys-primary);
      border: 2px solid var(--mat-sys-surface-container-low);
    }
    .timeline li.empty::before {
      background: var(--mat-sys-outline);
    }
    .meta {
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-label-medium);
    }
    .what {
      margin-block: 2px;
    }
    .comment {
      font-style: italic;
      margin-block: 4px;
    }
  `,
})
export class VoteTimeline {
  readonly voteId = input.required<number>();
  readonly pageSize = input(50);

  /** The page being read; undefined for the newest. */
  protected readonly before = linkedSignal<number, number | undefined>({
    source: this.voteId,
    computation: () => undefined,
  });
  protected readonly page = getVoteTimelineResource(
    this.voteId,
    computed(() => ({ before: this.before(), pageSize: this.pageSize() })),
  );

  /** Every page read so far, newest first. */
  protected readonly events = linkedSignal<TimelineEventDto[] | undefined, TimelineEventDto[]>({
    source: () => (this.page.hasValue() ? this.page.value()?.items : undefined),
    computation: (items, previous) => {
      if (!items) return previous?.value ?? [];
      return this.before() === undefined ? items : [...(previous?.value ?? []), ...items];
    },
  });
  protected readonly nextBefore = computed(() =>
    this.page.hasValue() ? this.page.value()?.nextBefore : null,
  );
  protected readonly describe = describeEvent;
}
