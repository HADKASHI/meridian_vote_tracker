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
}

/** The filter a `?state=` query param names, or pending. */
export function toFilter(state: string | undefined): ConflictFilter {
  return Object.values(ConflictFilter).includes(state as ConflictFilter) ? (state as ConflictFilter) : ConflictFilter.pending;
}
