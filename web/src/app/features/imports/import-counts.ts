import { OutcomeCountsDto } from '../../api/model';

/** The outcomes in the order the report lists them, with their glossary words. */
export const OUTCOMES = ['new', 'unchanged', 'duplicate', 'invalid', 'contradiction', 'conflict'] as const;

/** The outcomes a file produced, in report order, skipping those with no rows. */
export function outcomesWithRows(counts: OutcomeCountsDto): (typeof OUTCOMES)[number][] {
  return OUTCOMES.filter((o) => counts[o] > 0);
}
