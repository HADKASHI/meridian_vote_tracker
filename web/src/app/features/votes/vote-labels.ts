import { PositionDto } from '../../api/model';

/** A position as the votes screens show it: "Yes · weight 4", or "No position". */
export function positionLabel(position: PositionDto | null | undefined): string {
  return position ? `${position.stance === 'yes' ? 'Yes' : 'No'} · weight ${position.weight}` : 'No position';
}
