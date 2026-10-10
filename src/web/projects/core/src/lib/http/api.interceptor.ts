import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { DOCUMENT, inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { RuntimeConfigService } from '../config/runtime-config';
import { toProblemError } from './problem';
import { newTrace } from './trace';

const PLATFORM_PATHS = ['/v1/', '/health/'];

/** Whether a request URL is a call to the platform (written as `/v1/...` or `/health/...`). */
export function isPlatformCall(url: string): boolean {
  return PLATFORM_PATHS.some((prefix) => url.startsWith(prefix));
}

/**
 * Every call to the platform goes through here: the API base address is added, the request
 * gets its reference (`traceparent`) and the active language, and any failure is turned into
 * a ScsProblemError (contracts/frontend-runtime.md §6).
 */
export const apiInterceptor: HttpInterceptorFn = (request, next) => {
  if (!isPlatformCall(request.url)) {
    return next(request);
  }

  const config = inject(RuntimeConfigService).value;
  const language = inject(DOCUMENT).documentElement.lang === 'en' ? 'en' : 'ar';
  const trace = newTrace();

  const call = request.clone({
    url: config.apiBaseUrl + request.url,
    setHeaders: { traceparent: trace.traceparent, 'Accept-Language': language },
  });

  return next(call).pipe(
    catchError((error: unknown) =>
      throwError(() =>
        error instanceof HttpErrorResponse ? toProblemError(error, trace.traceId) : error,
      ),
    ),
  );
};
