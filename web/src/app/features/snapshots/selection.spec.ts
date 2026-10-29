import { categoryLabel, publicationBadge } from './selection';

describe('snapshot words', () => {
  it('shows the current publication, else the superseded quarters', () => {
    expect(publicationBadge([])).toBeNull();
    expect(publicationBadge([{ quarter: '2024-Q1', supersededByPublicationId: null }])).toBe('Published 2024-Q1');
    expect(publicationBadge([{ quarter: '2024-Q1', supersededByPublicationId: 9 }])).toBe('Superseded 2024-Q1');
  });

  it('uses the glossary words for categories', () => {
    expect(categoryLabel('non_member')).toBe('non-member');
    expect(categoryLabel('no_data')).toBe('no data');
  });
});
