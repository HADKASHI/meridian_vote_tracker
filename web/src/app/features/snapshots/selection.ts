import { SnapshotSelectionDto, VoteRefDto } from '../../api/model';

/** How a snapshot's votes are chosen: one mode per snapshot. */
export type SelectionMode = 'range' | 'sessions' | 'list';

export interface SelectionForm {
  mode: SelectionMode;
  from: string;
  to: string;
  /** Session numbers as typed, separated by commas or spaces. */
  sessions: string;
  /** Vote ids as typed, separated by commas or spaces. */
  voteIds: string;
  /** Votes taken out with "Remove these votes". */
  removed: VoteRefDto[];
}

/** The request body for the form. Unparseable numbers are sent as they are, so the API names the field. */
export function toSelection(form: SelectionForm): SnapshotSelectionDto {
  const excludedVoteIds = form.removed.length > 0 ? form.removed.map((v) => v.voteId) : null;
  switch (form.mode) {
    case 'range':
      return { from: form.from || null, to: form.to || null, excludedVoteIds };
    case 'sessions':
      return { sessions: numbers(form.sessions), excludedVoteIds };
    case 'list':
      return { voteIds: numbers(form.voteIds), excludedVoteIds };
  }
}

/** "2024-01-01 → 2024-03-31, minus MA/2024/007, MA/2024/041", as the snapshot records it. */
export function describeSelection(form: SelectionForm): string {
  const base =
    form.mode === 'range'
      ? `${form.from} → ${form.to}`
      : form.mode === 'sessions'
        ? `Sessions ${numbers(form.sessions).join(', ')}`
        : `${numbers(form.voteIds).length} chosen votes`;
  return form.removed.length > 0 ? `${base}, minus ${form.removed.map((v) => v.assemblyVoteId).join(', ')}` : base;
}

function numbers(text: string): number[] {
  return text
    .split(/[\s,]+/)
    .filter((part) => part.length > 0)
    .map(Number);
}

/** A stored selection as the snapshot page shows it; removed votes are counted, their ids are on each vote's timeline. */
export function describeStoredSelection(selection: SnapshotSelectionDto): string {
  const base =
    selection.from || selection.to
      ? `${selection.from} → ${selection.to}`
      : selection.sessions
        ? `Sessions ${selection.sessions.join(', ')}`
        : `${selection.voteIds?.length ?? 0} chosen votes`;
  const removed = selection.excludedVoteIds?.length ?? 0;
  return removed > 0 ? `${base}, minus ${removed} removed ${removed === 1 ? 'vote' : 'votes'}` : base;
}

/** A line's category in the glossary's words. */
export function categoryLabel(category: string): string {
  return category.replace('_', ' ').replace('non member', 'non-member');
}

/** The publication badge: the quarter a snapshot is the official index for, or was. */
export function publicationBadge(publications: { quarter: string; supersededByPublicationId: number | null }[]): string | null {
  const current = publications.find((p) => p.supersededByPublicationId === null);
  if (current) return `Published ${current.quarter}`;
  return publications.length > 0 ? `Superseded ${publications.map((p) => p.quarter).join(', ')}` : null;
}

/** The calendar quarter of a date, as a reporting quarter: 2024-Q2. */
export function quarterOf(date: Date): string {
  return `${date.getFullYear()}-Q${Math.floor(date.getMonth() / 3) + 1}`;
}
