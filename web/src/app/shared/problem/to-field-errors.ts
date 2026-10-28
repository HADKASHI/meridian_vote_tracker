import { Problem, ProblemType } from './problem';

export interface FieldError<TField> {
  field: TField;
  path: string;
  message: string;
}

export interface MappedErrors<TField> {
  /** Messages for fields the form knows, to show on those fields. */
  fieldErrors: FieldError<TField>[];
  /** Messages for no field or an unknown one, to show in the form's banner. */
  formErrors: string[];
}

/**
 * Maps a `validation` problem's errors, keyed by camelCase JSON path (`selection.to`,
 * `sessions[1]`), to the form's fields. `resolve` returns the field for a path, or
 * undefined when the form has none; such messages go to the banner instead of being lost.
 */
export function problemToFieldErrors<TField>(
  problem: Problem | null,
  resolve: (path: string) => TField | undefined,
): MappedErrors<TField> {
  const mapped: MappedErrors<TField> = { fieldErrors: [], formErrors: [] };
  if (!problem) return mapped;
  if (problem.type !== ProblemType.validation || !problem.errors) {
    mapped.formErrors.push(problem.detail ?? problem.title);
    return mapped;
  }
  for (const [path, messages] of Object.entries(problem.errors)) {
    const field = path === '' ? undefined : resolve(path);
    for (const message of messages) {
      if (field === undefined) mapped.formErrors.push(message);
      else mapped.fieldErrors.push({ field, path, message });
    }
  }
  return mapped;
}
