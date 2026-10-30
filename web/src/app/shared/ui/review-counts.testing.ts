import { HttpTestingController } from '@angular/common/http/testing';

/**
 * Answers the two count calls the Review tabs make (the shell status and the sheets waiting for review), so a
 * spec of a page that shows the tabs can wait for stability. Safe to call when neither is pending.
 */
export function flushReviewTabs(http: HttpTestingController, counts: { conflicts?: number; extractions?: number } = {}): void {
  http.match('/api/status').forEach((request) =>
    request.flush({ running: [], queued: [], recentlyFinished: [], pendingConflicts: counts.conflicts ?? 0 }));
  http.match((r) => r.url === '/api/extractions' && r.params.get('status') === 'needs_review').forEach((request) =>
    request.flush({ items: [], total: counts.extractions ?? 0, page: 1, pageSize: 1 }));
}
