import { OutcomeCountsDto } from '../../api/model';

/** The outcomes in the order the report lists them, with their glossary words. */
export const OUTCOMES = ['new', 'unchanged', 'duplicate', 'invalid', 'contradiction', 'conflict'] as const;

/** "5 new · 2 invalid": the outcomes a file produced, skipping those with no rows. */
export function countsSummary(counts: OutcomeCountsDto): string {
  const parts = OUTCOMES.filter((o) => counts[o] > 0).map((o) => `${counts[o].toLocaleString('en')} ${o}`);
  return parts.length > 0 ? parts.join(' · ') : 'No rows yet';
}
