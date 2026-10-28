import { OutcomeCountsDto, TimelineEventDto, TimelineValueDto } from '../../api/model';
import { ballotLabel } from './vote-labels';

/** A value on the timeline: a country vote, vote details or a position, whichever it carries. */
export function valueLabel(value: TimelineValueDto | null | undefined): string {
  if (!value) return '—';
  if (value.ballot) return ballotLabel(value.ballot);
  if (value.stance) return `${value.stance === 'yes' ? 'Yes' : 'No'} · weight ${value.weight}`;
  return `session ${value.session}, ${value.voteDate}, “${value.title}”`;
}

const outcomes: (keyof OutcomeCountsDto)[] = [
  'new',
  'unchanged',
  'duplicate',
  'invalid',
  'contradiction',
  'conflict',
];

/** "3 new, 1 conflict": only the outcomes the import gave this vote. */
export function countsLabel(counts: OutcomeCountsDto | null | undefined): string {
  return outcomes
    .filter((o) => counts?.[o])
    .map((o) => `${counts![o]} ${o}`)
    .join(', ');
}

function subjectLabel(event: TimelineEventDto): string {
  return event.subject === 'ballot' ? `the ${event.stateCode} country vote` : 'the vote details';
}

/** One line per timeline event, in the glossary's words. */
export function describeEvent(event: TimelineEventDto): string {
  switch (event.kind) {
    case 'import':
      return `Import #${event.importId} (${event.fileName}): ${countsLabel(event.counts)}`;
    case 'details_revision':
      return `Vote details approved: ${valueLabel(event.to)}`;
    case 'country_vote_revision':
      return `${event.stateCode} country vote approved: ${valueLabel(event.to)}`;
    case 'position_revision':
      return `Position set: ${valueLabel(event.to)}`;
    case 'conflict_opened':
      return `Conflict #${event.conflictId} opened on ${subjectLabel(event)} by import #${event.importId} (${event.fileName}): incoming ${valueLabel(event.to)}`;
    case 'conflict_decided':
      return `Conflict #${event.conflictId} on ${subjectLabel(event)} ${event.decision === 'accept' ? 'accepted' : 'rejected (current value kept)'}`;
    case 'decision_not_applied':
      return `Decision to ${event.decision} conflict #${event.conflictId} not applied: it was already decided`;
    case 'snapshot':
      return `Used in snapshot “${event.snapshotName}”`;
    case 'publication':
      return event.reason
        ? `Snapshot “${event.snapshotName}” published for ${event.quarter}, replacing the earlier publication: ${event.reason}`
        : `Snapshot “${event.snapshotName}” published for ${event.quarter}`;
  }
}
