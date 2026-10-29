import { SnapshotResultDto } from '../../api/model';

/** ADR 0007: how the ranking is narrowed. The ranks themselves never change. */
export type ConfidenceFilter = 'all' | 'high' | 'medium' | 'low' | 'none';
export type ConfidenceOrder = 'rank' | 'confidence';

/** A share (0–1) as a whole percent, or a dash for a state that was never a member. */
export function percent(share: number | null): string {
  return share === null ? '–' : `${Math.round(share * 100)}%`;
}

export function confidenceLabel(confidence: SnapshotResultDto['confidence']): string {
  return confidence === null ? 'No band' : confidence[0].toUpperCase() + confidence.slice(1);
}

export const isLowEvidence = (result: Pick<SnapshotResultDto, 'confidence'>): boolean => result.confidence === 'low';

const strength = { high: 0, medium: 1, low: 2 } as const;

/**
 * The results to show: filtered by band, and either in the stored order (rank, then code) or High first, keeping the
 * stored order within a band. It returns new arrays and leaves each result, and so its rank, as it was stored.
 */
export function narrow(
  results: readonly SnapshotResultDto[], filter: ConfidenceFilter, order: ConfidenceOrder,
): SnapshotResultDto[] {
  const kept = results.filter((r) => filter === 'all' || (filter === 'none' ? r.confidence === null : r.confidence === filter));
  if (order === 'rank') return kept;
  const byStrength = (r: SnapshotResultDto): number => (r.confidence === null ? 3 : strength[r.confidence]);
  return kept
    .map((result, index) => ({ result, index }))
    .sort((a, b) => byStrength(a.result) - byStrength(b.result) || a.index - b.index)
    .map((entry) => entry.result);
}
