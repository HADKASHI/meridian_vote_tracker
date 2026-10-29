import { TestBed } from '@angular/core/testing';
import { JobState, Outcome } from '../../api/model';
import { JobStateBadge } from './job-state-badge';
import { OutcomeChip } from './outcome-chip';

describe('status chips', () => {
  it.each(Object.values(Outcome))('an outcome chip says %s in the glossary word', async (outcome) => {
    const fixture = TestBed.createComponent(OutcomeChip);
    fixture.componentRef.setInput('outcome', outcome);
    await fixture.whenStable();

    expect((fixture.nativeElement as HTMLElement).textContent?.trim()).toBe(outcome);
  });

  it.each(Object.values(JobState))('a job-state chip says %s', async (state) => {
    const fixture = TestBed.createComponent(JobStateBadge);
    fixture.componentRef.setInput('state', state);
    await fixture.whenStable();

    expect((fixture.nativeElement as HTMLElement).textContent?.trim()).toBe(state);
  });

  it('a count beside an outcome is part of the chip', async () => {
    const fixture = TestBed.createComponent(OutcomeChip);
    fixture.componentRef.setInput('outcome', Outcome.conflict);
    fixture.componentRef.setInput('count', 1304);
    await fixture.whenStable();

    expect((fixture.nativeElement as HTMLElement).textContent?.replace(/\s+/g, ' ').trim()).toBe('conflict 1,304');
  });
});
