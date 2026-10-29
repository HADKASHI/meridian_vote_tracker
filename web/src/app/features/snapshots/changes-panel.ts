import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { DatePipe, NgTemplateOutlet } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ChangeOriginDto, SnapshotChangesDto } from '../../api/model';
import { getSnapshotChangesResource } from '../../api/snapshots/snapshots.resource';
import { LoadState } from '../../shared/ui/load-state';

interface Details {
  session: number;
  voteDate: string;
  title: string;
}

/** The most votes the API lists for each kind. */
const LISTED = 20;

/**
 * Extra (story 103): what would differ if the snapshot were created now. A live read of the stored
 * revisions against today's values: no score, no rank and nothing recalculated.
 */
@Component({
  selector: 'app-changes-panel',
  imports: [DatePipe, NgTemplateOutlet, RouterLink, LoadState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="surface-card" aria-labelledby="changes-heading" data-testid="snapshot-changes">
      <h2 id="changes-heading">Changes since it was calculated</h2>
      @if (changes.hasValue() && changes.value(); as c) {
        <p class="muted" data-testid="drift-live">
          <span class="chip" data-tone="outline">Live</span> read {{ c.readAt | date: 'yyyy-MM-dd HH:mm' }}: what would differ if this
          snapshot were created now.
        </p>
        @if (empty(c)) {
          <p data-testid="drift-nothing">Nothing has changed since this snapshot was calculated.</p>
        }
        @if (c.positionChanged.count > 0) {
          <section data-testid="drift-position">
            <h3>Positions changed ({{ c.positionChanged.count }})</h3>
            <ul>
              @for (v of c.positionChanged.votes; track v.vote.voteId) {
                <li>
                  <a [routerLink]="['/votes', v.vote.voteId]">{{ v.vote.assemblyVoteId }}</a>
                  {{ v.old.stance }}, weight {{ v.old.weight }} → <strong>{{ v.new.stance }}, weight {{ v.new.weight }}</strong>
                  by {{ origin(v.changedBy) }}
                </li>
              }
            </ul>
            <ng-container *ngTemplateOutlet="more; context: { count: c.positionChanged.count }" />
          </section>
        }
        @if (c.countryVotesChanged.count > 0) {
          <section data-testid="drift-country-votes">
            <h3>Country votes changed ({{ c.countryVotesChanged.count }})</h3>
            <ul>
              @for (v of c.countryVotesChanged.votes; track $index) {
                <li>
                  <a [routerLink]="['/votes', v.vote.voteId]">{{ v.vote.assemblyVoteId }}</a> {{ v.stateCode }}
                  {{ ballot(v.old) }} → <strong>{{ ballot(v.new) }}</strong> by {{ origin(v.changedBy) }}
                </li>
              }
            </ul>
            <ng-container *ngTemplateOutlet="more; context: { count: c.countryVotesChanged.count }" />
          </section>
        }
        @if (c.detailsChanged.count > 0) {
          <section data-testid="drift-details">
            <h3>Vote details changed ({{ c.detailsChanged.count }})</h3>
            <ul>
              @for (v of c.detailsChanged.votes; track v.vote.voteId) {
                <li>
                  <a [routerLink]="['/votes', v.vote.voteId]">{{ v.vote.assemblyVoteId }}</a> @for (d of differences(v.old, v.new); track d.field; let last = $last) {
                    {{ d.field }} {{ d.from }} → <strong>{{ d.to }}</strong>{{ last ? '' : ',' }}
                  }
                  by {{ origin(v.changedBy) }}
                </li>
              }
            </ul>
            <ng-container *ngTemplateOutlet="more; context: { count: c.detailsChanged.count }" />
          </section>
        }
        @if (c.nowSelected.count > 0) {
          <section data-testid="drift-now-selected">
            <h3>Now selected, not in the snapshot ({{ c.nowSelected.count }})</h3>
            <ul>
              @for (v of c.nowSelected.votes; track v.vote.voteId) {
                <li>
                  <a [routerLink]="['/votes', v.vote.voteId]">{{ v.vote.assemblyVoteId }}</a>
                  {{ v.details.title }}, {{ v.details.voteDate }},
                  {{ v.position ? v.position.stance + ', weight ' + v.position.weight : 'no position yet' }}
                </li>
              }
            </ul>
            <ng-container *ngTemplateOutlet="more; context: { count: c.nowSelected.count }" />
          </section>
        }
        @if (c.noLongerSelected.count > 0) {
          <section data-testid="drift-no-longer-selected">
            <h3>No longer selected ({{ c.noLongerSelected.count }})</h3>
            <ul>
              @for (v of c.noLongerSelected.votes; track v.vote.voteId) {
                <li>
                  <a [routerLink]="['/votes', v.vote.voteId]">{{ v.vote.assemblyVoteId }}</a> @for (d of differences(v.old, v.new); track d.field; let last = $last) {
                    {{ d.field }} {{ d.from }} → <strong>{{ d.to }}</strong>{{ last ? '' : ',' }}
                  }
                  by {{ origin(v.changedBy) }}
                </li>
              }
            </ul>
            <ng-container *ngTemplateOutlet="more; context: { count: c.noLongerSelected.count }" />
          </section>
        }
      } @else if (changes.error()) {
        <app-load-state kind="error" message="The changes could not be read." />
      } @else {
        <app-load-state kind="loading" message="Reading the changes" />
      }
    </section>
    <ng-template #more let-count="count">
      @if (count > listed) {
        <p class="muted">The first {{ listed }} are listed.</p>
      }
    </ng-template>
  `,
  styles: `
    h2 {
      font: var(--mat-sys-title-medium);
      margin: 0 0 4px;
    }
    h3 {
      font: var(--mat-sys-title-small);
      margin: 12px 0 4px;
    }
    ul {
      margin: 0;
      padding-inline-start: 20px;
    }
    li {
      overflow-wrap: anywhere;
    }
  `,
})
export class ChangesPanel {
  readonly snapshotId = input.required<number>();

  protected readonly listed = LISTED;
  protected readonly changes = getSnapshotChangesResource(this.snapshotId);

  protected empty(c: SnapshotChangesDto): boolean {
    return [c.positionChanged, c.countryVotesChanged, c.detailsChanged, c.nowSelected, c.noLongerSelected].every((kind) => kind.count === 0);
  }

  protected ballot(ballot: string | null): string {
    return ballot ? ballot.toUpperCase() : 'none';
  }

  /** Only the parts of the details that differ, so a corrected date reads as a date change. */
  protected differences(from: Details, to: Details): { field: string; from: string; to: string }[] {
    return [
      { field: 'date', from: from.voteDate, to: to.voteDate },
      { field: 'session', from: String(from.session), to: String(to.session) },
      { field: 'title', from: from.title, to: to.title },
    ].filter((d) => d.from !== d.to);
  }

  protected origin(o: ChangeOriginDto): string {
    switch (o.kind) {
      case 'position':
        return `${o.by} (set position)`;
      case 'import':
        return `${o.by} (import #${o.importId})`;
      case 'decision':
        return `${o.by} (decision #${o.decisionId})`;
    }
  }
}
