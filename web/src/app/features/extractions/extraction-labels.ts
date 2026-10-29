import { Ballot, ExtractionStatus, SheetIssue, SheetIssueSeverity, SheetTotals } from '../../api/model';

/** An extraction's status in the glossary's words. */
export function statusLabel(status: ExtractionStatus): string {
  switch (status) {
    case ExtractionStatus.extracting:
      return 'Reading';
    case ExtractionStatus.needs_review:
      return 'Needs review';
    case ExtractionStatus.failed:
      return 'Failed';
    case ExtractionStatus.approved:
      return 'Approved';
    case ExtractionStatus.rejected:
      return 'Rejected';
  }
}

export function statusTone(status: ExtractionStatus): string {
  switch (status) {
    case ExtractionStatus.extracting:
      return 'outline';
    case ExtractionStatus.needs_review:
      return 'tertiary';
    case ExtractionStatus.failed:
      return 'error';
    case ExtractionStatus.approved:
      return 'secondary';
    case ExtractionStatus.rejected:
      return 'outline';
  }
}

/** How often a sheet being read is asked about again, on the list and on the sheet itself. */
export const READING_POLL_MS = 1000;

/** Sheets still to deal with: being read, waiting, or failed. */
export const OPEN_STATUSES: ExtractionStatus[] = [ExtractionStatus.extracting, ExtractionStatus.needs_review, ExtractionStatus.failed];

/** The four ballots as a sheet's columns show them: a letter and what it means. */
export const BALLOT_CHOICES = [
  { value: Ballot.y, label: 'Y · yes' },
  { value: Ballot.n, label: 'N · no' },
  { value: Ballot.a, label: 'A · abstain' },
  { value: Ballot.x, label: 'X · did not vote' },
] as const;

export function ballotLabel(ballot: Ballot | null): string {
  return ballot ? ballot.toUpperCase() : '—';
}

/** The totals a sheet prints and its rows add up to, as columns. */
export const TOTAL_COLUMNS = [
  { key: 'yes', label: 'Yes' },
  { key: 'no', label: 'No' },
  { key: 'abstentions', label: 'Abstentions' },
  { key: 'notVoting', label: 'Not voting' },
] as const;

export type TotalKey = (typeof TOTAL_COLUMNS)[number]['key'];

export function totalOf(t: SheetTotals): number {
  return t.yes + t.no + t.abstentions + t.notVoting;
}

export function isBlocking(issue: SheetIssue): boolean {
  return issue.severity === SheetIssueSeverity.blocking;
}
