import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { getConflictResource } from '../../api/conflicts/conflicts.resource';
import { ConflictDto, ConflictValueDto } from '../../api/model';
import { decisionLabel, originLabel, subjectLabel, valueLabel } from './conflict-values';

/** The decision pane: both values with their origins, so the analyst decides on evidence. */
@Component({
  selector: 'app-conflict-page',
  imports: [DatePipe, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <article data-testid="decision-pane">
      <h2>Conflict #{{ conflictId() }}</h2>
      @if (conflict(); as c) {
        <p class="about">
          <a [routerLink]="['/votes', c.vote.voteId]">{{ c.vote.assemblyVoteId }}</a> · {{ subject(c) }}
        </p>
        <div class="sides">
          <section data-testid="current">
            <h3>Current</h3>
            @if (c.current; as current) {
              <p class="value">{{ value(c, current.value) }}</p>
              <p class="origin">{{ origin(current.origin) }}</p>
            } @else {
              <p class="value">—</p>
            }
          </section>
          <section data-testid="incoming">
            <h3>Incoming</h3>
            <p class="value">{{ value(c, c.incoming.value) }}</p>
            <p class="origin">{{ origin(c.incoming.origin) }}</p>
          </section>
        </div>
        @if (c.decision; as d) {
          <section class="decided" data-testid="decision">
            <p>
              <strong>{{ decision(d.decision) }} by {{ d.decidedBy }}</strong> at {{ d.decidedAt | date: 'yyyy-MM-dd HH:mm' }}:
              <span class="value">{{ value(c, d.from) }} → {{ value(c, d.to) }}</span>
            </p>
            @if (d.comment) {
              <blockquote>{{ d.comment }}</blockquote>
            }
          </section>
        }
        @if (c.notApplied.length > 0) {
          <ul class="not-applied" data-testid="not-applied">
            @for (a of c.notApplied; track a.id) {
              <li>{{ a.attemptedBy }} tried to {{ a.attempted }}; not applied ({{ a.attemptedAt | date: 'yyyy-MM-dd HH:mm:ss' }})</li>
            }
          </ul>
        }
      } @else if (resource.error()) {
        <p>This conflict could not be loaded.</p>
      }
    </article>
  `,
  styles: `
    .sides {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 16px;
    }
    .sides section {
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: 8px;
      padding: 8px 16px;
    }
    h3 {
      margin-block: 0 4px;
      font-size: 0.95em;
      opacity: 0.8;
    }
    .value {
      font-family: monospace;
      font-size: 1.1em;
    }
    .origin,
    .not-applied {
      opacity: 0.75;
      font-size: 0.9em;
    }
    .decided {
      margin-block-start: 16px;
    }
    blockquote {
      margin: 4px 0 0;
      padding-inline-start: 12px;
      border-inline-start: 3px solid var(--mat-sys-outline-variant);
    }
  `,
})
export class ConflictPage {
  readonly conflictId = input.required<string>();
  protected readonly resource = getConflictResource(computed(() => Number(this.conflictId())));
  protected readonly conflict = computed<ConflictDto | undefined>(() =>
    this.resource.hasValue() ? this.resource.value() : undefined,
  );
  protected readonly subject = subjectLabel;
  protected readonly origin = originLabel;
  protected readonly decision = decisionLabel;

  protected value(c: ConflictDto, value: ConflictValueDto | null): string {
    return valueLabel(c.subject, value);
  }
}
