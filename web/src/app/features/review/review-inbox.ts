import { computed, Injectable, Signal, signal } from '@angular/core';
import { listConflictsResource } from '../../api/conflicts/conflicts.resource';
import { ConflictFilter, ConflictListItemDto, ListConflictsParams } from '../../api/model';

/** Some votes or one import the inbox is narrowed to, as links from other screens name them. */
export interface InboxScope {
  voteIds: number[];
  importId?: number;
}

/** The most conflicts the inbox shows at once; the API's largest page. */
export const INBOX_PAGE_SIZE = 200;

/**
 * The Review inbox's list, shared by the list and the decision pane beside it, so a
 * decision can reload the list and "Next conflict" can follow its order. Provided per
 * `/review` route.
 */
@Injectable()
export class ReviewInbox {
  private readonly source = signal<{ filter: Signal<ConflictFilter>; scope: Signal<InboxScope> } | null>(null);
  readonly filter = computed(() => this.source()?.filter() ?? ConflictFilter.pending);
  readonly scope = computed(() => this.source()?.scope() ?? { voteIds: [] }, { equal: sameScope });
  readonly narrowed = computed(() => this.scope().voteIds.length > 0 || this.scope().importId !== undefined);
  /** The query params that keep the inbox as it is when moving between conflicts. */
  readonly queryParams = computed(() => ({
    state: this.filter(),
    ...(this.scope().voteIds.length > 0 ? { voteIds: this.scope().voteIds } : {}),
    ...(this.scope().importId !== undefined ? { importId: this.scope().importId } : {}),
  }));
  readonly list = listConflictsResource(
    computed<ListConflictsParams>(() => ({ ...this.queryParams(), pageSize: INBOX_PAGE_SIZE })),
  );
  readonly items = computed<ConflictListItemDto[]>(() => (this.list.hasValue() ? this.list.value()?.items ?? [] : []));
  readonly total = computed(() => (this.list.hasValue() ? this.list.value()?.total ?? 0 : 0));

  /** Reads the filter and scope from the page's query params, before the list is first loaded. */
  bind(filter: Signal<ConflictFilter>, scope: Signal<InboxScope>): void {
    this.source.set({ filter, scope });
  }

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

/** The scope `?voteIds=` (repeated) and `?importId=` name; anything that isn't an id is ignored. */
export function toScope(voteIds: string | string[] | undefined, importId: string | undefined): InboxScope {
  const ids = (Array.isArray(voteIds) ? voteIds : voteIds === undefined ? [] : [voteIds]).map(Number).filter(Number.isInteger);
  const importNo = Number(importId);
  return { voteIds: ids, ...(importId !== undefined && Number.isInteger(importNo) ? { importId: importNo } : {}) };
}

function sameScope(a: InboxScope, b: InboxScope): boolean {
  return a.importId === b.importId && a.voteIds.length === b.voteIds.length && a.voteIds.every((id, i) => id === b.voteIds[i]);
}
