import { Ballot, PositionDto } from '../../api/model';

/** A position as the votes screens show it: "Yes · weight 4", or "No position". */
export function positionLabel(position: PositionDto | null | undefined): string {
  return position ? `${position.stance === 'yes' ? 'Yes' : 'No'} · weight ${position.weight}` : 'No position';
}

const ballotWords: Record<NonNullable<Ballot>, string> = { y: 'Y (yes)', n: 'N (no)', a: 'A (abstain)', x: 'X (absent)' };

/** A country vote as the glossary writes it: "Y (yes)", or "—" when nothing is approved. */
export function ballotLabel(ballot: Ballot | null | undefined): string {
  return ballot ? ballotWords[ballot] : '—';
}
