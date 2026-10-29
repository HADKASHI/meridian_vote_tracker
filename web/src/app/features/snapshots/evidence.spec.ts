import { SnapshotResultDto } from '../../api/model';
import { confidenceLabel, isLowEvidence, narrow, percent } from './evidence';

const result = (stateCode: string, rank: number | null, confidence: SnapshotResultDto['confidence']): SnapshotResultDto => ({
  stateCode, stateName: stateCode, matchedWeight: 0, countedWeight: 0, countableVotes: 0, score: null, rank,
  completeness: 1, abstentionRate: 0, missingData: 0, confidence,
});
const stored = [
  result('FRA', 1, 'low'), result('ARG', 2, 'high'), result('BRA', 2, 'medium'), result('JPN', 4, 'high'), result('CIV', null, null),
];

describe('evidence words', () => {
  it('shows a share as a whole percent, and nothing for a state that was never a member', () => {
    expect(percent(1)).toBe('100%');
    expect(percent(0.857)).toBe('86%');
    expect(percent(0)).toBe('0%');
    expect(percent(null)).toBe('–');
  });

  it('names the band, and a state with nothing countable has none', () => {
    expect(confidenceLabel('high')).toBe('High');
    expect(confidenceLabel('low')).toBe('Low');
    expect(confidenceLabel(null)).toBe('No band');
  });

  it('flags low evidence only for the Low band', () => {
    expect(stored.filter(isLowEvidence).map((r) => r.stateCode)).toEqual(['FRA']);
  });
});

describe('narrow', () => {
  it('keeps the stored order and every state when nothing is chosen', () => {
    expect(narrow(stored, 'all', 'rank').map((r) => r.stateCode)).toEqual(['FRA', 'ARG', 'BRA', 'JPN', 'CIV']);
  });

  it('filters by band, with a choice for the states that have none', () => {
    expect(narrow(stored, 'high', 'rank').map((r) => r.stateCode)).toEqual(['ARG', 'JPN']);
    expect(narrow(stored, 'low', 'rank').map((r) => r.stateCode)).toEqual(['FRA']);
    expect(narrow(stored, 'none', 'rank').map((r) => r.stateCode)).toEqual(['CIV']);
  });

  it('sorts High first then Medium, Low and none, keeping the stored order within a band, and never touches the ranks', () => {
    const sorted = narrow(stored, 'all', 'confidence');

    expect(sorted.map((r) => r.stateCode)).toEqual(['ARG', 'JPN', 'BRA', 'FRA', 'CIV']);
    expect(sorted.map((r) => r.rank)).toEqual([2, 4, 2, 1, null]);
    expect(stored.map((r) => r.stateCode)).toEqual(['FRA', 'ARG', 'BRA', 'JPN', 'CIV']);
  });
});
