import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { DOCUMENT, inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { SCS_APP } from '../app-token';
import { CLINIC_HEADER, clinicFromPath } from '../clinic/clinic.service';
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
 *
 * On the patient site only, the clinic named by the address is sent as `X-Scs-Clinic`. It says
 * which public clinic page is being viewed; it is not a credential. The staff and operators
 * sites never send it: there the clinic comes from the signed-in account.
 */
export const apiInterceptor: HttpInterceptorFn = (request, next) => {
  if (!isPlatformCall(request.url)) {
    return next(request);
  }

  const config = inject(RuntimeConfigService).value;
  const document = inject(DOCUMENT);
  const language = document.documentElement.lang === 'en' ? 'en' : 'ar';
  const trace = newTrace();

  const headers: Record<string, string> = {
    traceparent: trace.traceparent,
    'Accept-Language': language,
  };

  if (inject(SCS_APP, { optional: true }) === 'patient') {
    const clinic = clinicFromPath(document.location?.pathname ?? '/');
    if (clinic !== null) {
      headers[CLINIC_HEADER] = clinic;
    }
  }

  const call = request.clone({ url: config.apiBaseUrl + request.url, setHeaders: headers });

  return next(call).pipe(
    catchError((error: unknown) =>
      throwError(() =>
        error instanceof HttpErrorResponse ? toProblemError(error, trace.traceId) : error,
      ),
    ),
  );
};
