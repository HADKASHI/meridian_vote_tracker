import { TestBed } from '@angular/core/testing';
import { ScoreWithBasis, scoreBasis } from './score';

describe('scoreBasis', () => {
  it('names the countable votes and the weight behind a score', () => {
    expect(scoreBasis({ countableVotes: 10, matchedWeight: 20, countedWeight: 32 })).toBe('10 countable votes · 20 of 32 weight matched');
    expect(scoreBasis({ countableVotes: 1, matchedWeight: 0, countedWeight: 3 })).toBe('1 countable vote · 0 of 3 weight matched');
    expect(scoreBasis({ countableVotes: 0, matchedWeight: 0, countedWeight: 0 })).toBe('nothing countable');
  });
});

describe('ScoreWithBasis', () => {
  it('shows the score to 1 decimal beside its basis, and n/a when nothing is countable', async () => {
    const fixture = TestBed.createComponent(ScoreWithBasis);
    fixture.componentRef.setInput('result', { score: 62.5, countableVotes: 10, matchedWeight: 20, countedWeight: 32 });
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('.score')?.textContent?.trim()).toBe('62.5');
    expect(element.textContent).toContain('10 countable votes · 20 of 32 weight matched');

    fixture.componentRef.setInput('result', { score: null, countableVotes: 0, matchedWeight: 0, countedWeight: 0 });
    await fixture.whenStable();
    expect(element.querySelector('.score')?.textContent?.trim()).toBe('n/a');
  });
});
