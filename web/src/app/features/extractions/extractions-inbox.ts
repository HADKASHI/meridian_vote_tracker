import { computed, Injectable, Signal, signal } from '@angular/core';
import { listExtractionsResource } from '../../api/extractions/extractions.resource';
import { ExtractionSummaryDto, ListExtractionsParams } from '../../api/model';
import { OPEN_STATUSES } from './extraction-labels';

/** The most extractions the list shows at once; the API's largest page. */
export const EXTRACTIONS_PAGE_SIZE = 200;

export type ExtractionFilter = 'open' | 'decided' | 'all';

/** The filter a `?state=` query param names, or open. */
export function toFilter(state: string | undefined): ExtractionFilter {
  return state === 'decided' || state === 'all' ? state : 'open';
}

/**
 * The extractions list, shared by the list and the detail beside it, so a decision can reload it. Provided per
 * `/review/extractions` route. "Open" is what still needs doing (being read, waiting, failed), "decided" is the rest.
 */
@Injectable()
export class ExtractionsInbox {
  private readonly source = signal<Signal<ExtractionFilter> | null>(null);
  readonly filter = computed(() => this.source()?.() ?? 'open');
  readonly list = listExtractionsResource(signal<ListExtractionsParams>({ pageSize: EXTRACTIONS_PAGE_SIZE }));
  private readonly all = computed<ExtractionSummaryDto[]>(() => (this.list.hasValue() ? this.list.value()?.items ?? [] : []));
  readonly items = computed(() =>
    this.all().filter((item) => {
      const open = OPEN_STATUSES.includes(item.status);
      return this.filter() === 'all' || (this.filter() === 'open' ? open : !open);
    }),
  );
  readonly total = computed(() => (this.list.hasValue() ? this.list.value()?.total ?? 0 : 0));
  /** Reads the filter from the page's query param, before the list is first shown. */
  bind(filter: Signal<ExtractionFilter>): void {
    this.source.set(filter);
  }

  /** Something is still being read, so the list keeps asking. */
  readonly reading = computed(() => this.all().some((item) => item.status === 'extracting'));
}
