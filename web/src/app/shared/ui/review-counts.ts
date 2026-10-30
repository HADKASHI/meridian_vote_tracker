import { computed, inject, Injectable, Injector, runInInjectionContext, signal } from '@angular/core';
import { listExtractionsResource } from '../../api/extractions/extractions.resource';
import { ExtractionStatus, ListExtractionsParams } from '../../api/model';
import { ShellStatus } from '../../core/status/shell-status';
import { pollWhile } from '../http/poll';

/** How often the count of sheets waiting for review is read again while the tab is visible. */
const EXTRACTIONS_POLL_MS = 5000;

/**
 * What waits in the Review inbox, for its tabs: pending conflicts from the shell's status poll, and the extractions
 * waiting for review from one small list call (only its total is used).
 */
@Injectable({ providedIn: 'root' })
export class ReviewCounts {
  private readonly shell = inject(ShellStatus);
  private readonly extractionList = listExtractionsResource(
    signal<ListExtractionsParams>({ status: ExtractionStatus.needs_review, pageSize: 1 }),
  );

  readonly conflicts = this.shell.pendingConflicts;
  readonly extractions = computed(() => (this.extractionList.hasValue() ? this.extractionList.value()?.total ?? 0 : 0));

  constructor() {
    runInInjectionContext(inject(Injector), () => pollWhile(this.extractionList, EXTRACTIONS_POLL_MS, () => true));
  }

  /** After a decision or an upload, so the tabs don't wait for the next poll. */
  reload(): void {
    this.extractionList.reload();
    this.shell.status.reload();
  }
}
