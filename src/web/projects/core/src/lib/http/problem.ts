import { HttpErrorResponse } from '@angular/common/http';

/** The one shape of every platform failure (contracts/conventions.md §4). */
export interface ScsProblem {
  readonly type: string;
  readonly title: string;
  readonly status: number;
  readonly code: string;
  readonly traceId: string;
  readonly errors?: Readonly<Record<string, readonly string[]>>;
}

export const SERVICE_UNAVAILABLE = 'SERVICE_UNAVAILABLE';

/** Thrown for every failed call to the platform. The UI branches on `problem.code`. */
export class ScsProblemError extends Error {
  constructor(
    readonly problem: ScsProblem,
    /** True when the platform never answered and the problem was built in the browser. */
    readonly local: boolean,
    /** Seconds the platform asked the caller to wait (`Retry-After`), when it said so. */
    readonly retryAfterSeconds: number | null = null,
  ) {
    super(problem.code);
    this.name = 'ScsProblemError';
  }
}

export function isScsProblem(value: unknown): value is ScsProblem {
  if (typeof value !== 'object' || value === null) {
    return false;
  }
  const candidate = value as Record<string, unknown>;
  return (
    typeof candidate['code'] === 'string' &&
    typeof candidate['title'] === 'string' &&
    typeof candidate['status'] === 'number' &&
    typeof candidate['traceId'] === 'string'
  );
}

/**
 * Turns a failed response into a problem. Anything that is not the platform's own shape
 * (network failure, a proxy's error page) becomes SERVICE_UNAVAILABLE carrying the reference
 * the browser generated for the request.
 */
export function toProblemError(response: HttpErrorResponse, traceId: string): ScsProblemError {
  if (isScsProblem(response.error)) {
    const wait = Number.parseInt(response.headers?.get('Retry-After') ?? '', 10);
    return new ScsProblemError(
      response.error,
      false,
      Number.isFinite(wait) && wait > 0 ? wait : null,
    );
  }

  return new ScsProblemError(
    {
      type: 'urn:scs:error:service-unavailable',
      title: '',
      status: response.status,
      code: SERVICE_UNAVAILABLE,
      traceId,
    },
    true,
  );
}
