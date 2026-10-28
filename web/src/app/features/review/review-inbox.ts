import { computed, Injectable, signal } from '@angular/core';
import { listConflictsResource } from '../../api/conflicts/conflicts.resource';
import { ConflictFilter, ConflictListItemDto } from '../../api/model';

/** The most conflicts the inbox shows at once; the API's largest page. */
export const INBOX_PAGE_SIZE = 200;

/**
 * The Review inbox's list, shared by the list and the decision pane beside it, so a
 * decision can reload the list and "Next conflict" can follow its order. Provided per
 * `/review` route.
 */
@Injectable()
export class ReviewInbox {
  readonly filter = signal<ConflictFilter>(ConflictFilter.pending);
  readonly list = listConflictsResource(computed(() => ({ state: this.filter(), pageSize: INBOX_PAGE_SIZE })));
  readonly items = computed<ConflictListItemDto[]>(() => (this.list.hasValue() ? this.list.value()?.items ?? [] : []));
  readonly total = computed(() => (this.list.hasValue() ? this.list.value()?.total ?? 0 : 0));

  /** The ids after `id` in inbox order, then those before it: where "Next conflict" looks. */
  orderAfter(id: number): number[] {
    const ids = this.items().map((item) => item.id);
    const at = ids.indexOf(id);
    return at < 0 ? ids : [...ids.slice(at + 1), ...ids.slice(0, at)];
  }

  /** The first pending conflict in `order` that is still listed, else the first pending one. */
  nextPending(order: number[], exclude: number): ConflictListItemDto | undefined {
    const pending = this.items().filter((item) => !item.decision && item.id !== exclude);
    return order.map((id) => pending.find((item) => item.id === id)).find((item) => item !== undefined) ?? pending[0];
  }
}

/** The filter a `?state=` query param names, or pending. */
export function toFilter(state: string | undefined): ConflictFilter {
  return Object.values(ConflictFilter).includes(state as ConflictFilter) ? (state as ConflictFilter) : ConflictFilter.pending;
}
