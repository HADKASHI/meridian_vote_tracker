import { PositionDto } from '../../api/model';

/** "Yes · weight 4": a position in the words the worklist and its 409 card share. */
export function positionLabel(position: Pick<PositionDto, 'stance' | 'weight'>): string {
  return `${position.stance === 'yes' ? 'Yes' : 'No'} · weight ${position.weight}`;
}
