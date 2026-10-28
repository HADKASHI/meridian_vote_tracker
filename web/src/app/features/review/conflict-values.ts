import { ConflictListItemDto, ConflictSubject, ConflictValueDto, Decision, OriginDto, OriginKind } from '../../api/model';

/** A ballot as the Assembly writes it: Y, N, A or X. */
export function ballotLabel(value: ConflictValueDto | null | undefined): string {
  return value?.ballot ? value.ballot.toUpperCase() : '—';
}

/** Vote details on one line: session, date and title. */
export function detailsLabel(value: ConflictValueDto | null | undefined): string {
  return value ? `session ${value.session} · ${value.voteDate} · ${value.title}` : '—';
}

/** Either kind of value on one line. */
export function valueLabel(subject: ConflictSubject, value: ConflictValueDto | null | undefined): string {
  return subject === ConflictSubject.ballot ? ballotLabel(value) : detailsLabel(value);
}

/** What the conflict would change, as current → incoming; for details only the fields that differ. */
export function changeLabel(subject: ConflictSubject, current: ConflictValueDto | null, incoming: ConflictValueDto): string {
  if (subject === ConflictSubject.ballot || !current) return `${valueLabel(subject, current)} → ${valueLabel(subject, incoming)}`;
  const changes = [
    current.session !== incoming.session ? `session ${current.session} → ${incoming.session}` : null,
    current.voteDate !== incoming.voteDate ? `date ${current.voteDate} → ${incoming.voteDate}` : null,
    current.title !== incoming.title ? `title “${current.title}” → “${incoming.title}”` : null,
  ].filter((change) => change !== null);
  return changes.length > 0 ? changes.join('; ') : 'same as current';
}

export function subjectLabel(item: Pick<ConflictListItemDto, 'subject' | 'stateCode'>): string {
  return item.subject === ConflictSubject.ballot ? `Country vote · ${item.stateCode}` : 'Vote details';
}

export function decisionLabel(decision: Decision): string {
  return decision === Decision.accept ? 'Accepted' : 'Rejected';
}

/** Where a value came from: the file row that brought it, or the decision that approved it. */
export function originLabel(origin: OriginDto): string {
  return origin.kind === OriginKind.import_row
    ? `${origin.fileName} line ${origin.lineNo} · uploaded by ${origin.uploadedBy}`
    : `approved by ${origin.decidedBy} deciding conflict #${origin.conflictId}`;
}
