import { Problem } from './problem';
import { problemToFieldErrors } from './to-field-errors';

describe('problemToFieldErrors', () => {
  const fields = new Map([
    ['name', 'name field'],
    ['selection.to', 'to field'],
    ['selection.sessions[1]', 'second session field'],
  ]);
  const resolve = (path: string) => fields.get(path);

  it('puts each message on the field its camelCase JSON path names', () => {
    const problem: Problem = {
      type: 'validation', title: 'Invalid', status: 400, traceId: 'abc',
      errors: { name: ['Enter a name.'], 'selection.to': ['The last date is before the first.'], 'selection.sessions[1]': ['A session is a positive number.'] },
    };

    const mapped = problemToFieldErrors(problem, resolve);

    expect(mapped.fieldErrors).toEqual([
      { field: 'name field', path: 'name', message: 'Enter a name.' },
      { field: 'to field', path: 'selection.to', message: 'The last date is before the first.' },
      { field: 'second session field', path: 'selection.sessions[1]', message: 'A session is a positive number.' },
    ]);
    expect(mapped.formErrors).toEqual([]);
  });

  it('sends messages for unknown or no fields to the form banner', () => {
    const problem: Problem = {
      type: 'validation', title: 'Invalid', status: 400,
      errors: { '': ['Choose one of a date range, sessions or a list of votes.'], 'selection.from': ['Enter the first date.'] },
    };

    expect(problemToFieldErrors(problem, resolve).formErrors).toEqual([
      'Choose one of a date range, sessions or a list of votes.',
      'Enter the first date.',
    ]);
  });

  it('shows another problem as one form message', () => {
    const problem: Problem = { type: 'no-votes-match', title: 'No votes match', status: 400, detail: 'No vote is in 2030.' };

    expect(problemToFieldErrors(problem, resolve)).toEqual({ fieldErrors: [], formErrors: ['No vote is in 2030.'] });
  });
});
