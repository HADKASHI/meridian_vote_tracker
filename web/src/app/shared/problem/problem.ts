import { HttpErrorResponse } from '@angular/common/http';

/** The fixed catalogue of ProblemDetails types the API returns; the UI handles each one explicitly. */
export const ProblemType = {
  validation: 'validation',
  badRequest: 'bad-request',
  noVotesMatch: 'no-votes-match',
  noPosition: 'no-position',
  unknownUser: 'unknown-user',
  forbidden: 'forbidden',
  notFound: 'not-found',
  methodNotAllowed: 'method-not-allowed',
  alreadyDecided: 'already-decided',
  stalePosition: 'stale-position',
  quarterPublished: 'quarter-published',
  payloadTooLarge: 'payload-too-large',
  unexpected: 'unexpected',
  notImplemented: 'not-implemented',
} as const;

export type ProblemType = (typeof ProblemType)[keyof typeof ProblemType];

export interface Problem {
  type: ProblemType;
  title: string;
  status: number;
  detail?: string | null;
  traceId?: string;
  errors?: Record<string, string[]>;
  [extension: string]: unknown;
}

/** The ProblemDetails body of a failed call, or null when there is none (network error, HTML page). */
export function toProblem(error: unknown): Problem | null {
  const body = error instanceof HttpErrorResponse ? error.error : error;
  return body && typeof body === 'object' && typeof (body as Problem).type === 'string' ? (body as Problem) : null;
}

/** Narrows a problem to one type with its typed body, e.g. `isProblem<AlreadyDecidedProblem>(p, 'already-decided')`. */
export function isProblem<T extends object = Problem>(problem: Problem | null, type: ProblemType): problem is Problem & T {
  return problem?.type === type;
}
