import { PositionDto, UserDto } from '../../api/model';

/** "Yes · weight 4": a position in the words the worklist and its 409 card share. */
export function positionLabel(position: Pick<PositionDto, 'stance' | 'weight'>): string {
  return `${position.stance === 'yes' ? 'Yes' : 'No'} · weight ${position.weight}`;
}

/** Who set a position, by display name ("Lena"), or the user id while the user list loads. */
export function setByName(users: readonly UserDto[], position: Pick<PositionDto, 'setBy'>): string {
  return users.find((u) => u.id === position.setBy)?.displayName ?? position.setBy;
}
