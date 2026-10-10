import { HttpClient } from '@angular/common/http';
import {
  DOCUMENT,
  ErrorHandler,
  Injectable,
  InjectionToken,
  Injector,
  inject,
} from '@angular/core';
import { ActivatedRouteSnapshot, Router } from '@angular/router';
import { ScsProblemError } from '../http/problem';
import { SCS_APP } from '../app-token';
import { VersionCheckService } from '../version/version-check.service';

const MESSAGE_MAX = 1000;
const STACK_MAX = 8000;
const ROUTE_MAX = 200;
const VERSION_MAX = 40;
const REPEAT_WINDOW_MS = 60_000;

/** The clock; replaced in tests. */
export const ERROR_CLOCK = new InjectionToken<() => number>('ERROR_CLOCK', {
  providedIn: 'root',
  factory: () => () => Date.now(),
});

/** The body of `POST /v1/client-errors` (contracts/platform-api.openapi.yaml). */
export interface ClientErrorReport {
  app: string;
  version: string;
  message: string;
  route?: string;
  stack?: string;
  language?: string;
}

/**
 * Reports unhandled failures to the platform (FR-025). It sends the route *template*, never
 * the address with its values, and never anything the user typed. The same message is reported
 * at most once a minute per tab.
 */
@Injectable()
export class ScsErrorHandler implements ErrorHandler {
  // Resolved lazily: the router and HTTP client themselves depend on the error handler.
  private readonly injector = inject(Injector);
  private readonly app = inject(SCS_APP);
  private readonly now = inject(ERROR_CLOCK);
  private readonly document = inject(DOCUMENT);
  private readonly lastSent = new Map<string, number>();

  handleError(error: unknown): void {
    console.error(error);

    try {
      const report = this.describe(error);
      const previous = this.lastSent.get(report.message);
      const time = this.now();
      if (previous !== undefined && time - previous < REPEAT_WINDOW_MS) {
        return;
      }
      this.lastSent.set(report.message, time);

      // A failure to report must never raise another report.
      this.injector
        .get(HttpClient)
        .post('/v1/client-errors', report)
        .subscribe({ error: () => undefined });
    } catch {
      // Reporting is best effort.
    }
  }

  private describe(error: unknown): ClientErrorReport {
    const report: ClientErrorReport = {
      app: this.app,
      version: (this.injector.get(VersionCheckService).current()?.version ?? 'unknown').slice(
        0,
        VERSION_MAX,
      ),
      message: this.messageOf(error).slice(0, MESSAGE_MAX) || 'Unknown error',
      language: this.document.documentElement.lang === 'en' ? 'en' : 'ar',
    };

    const route = this.routeTemplate();
    if (route) {
      report.route = route.slice(0, ROUTE_MAX);
    }

    if (error instanceof Error && error.stack && !(error instanceof ScsProblemError)) {
      report.stack = error.stack.slice(0, STACK_MAX);
    }

    return report;
  }

  private messageOf(error: unknown): string {
    if (error instanceof ScsProblemError) {
      return `Unhandled platform failure ${error.problem.code} (${error.problem.traceId})`;
    }
    if (error instanceof Error) {
      return `${error.name}: ${error.message}`;
    }
    return typeof error === 'string' ? error : 'Non-error value thrown';
  }

  /** `/patients/:id`, built from the route configuration rather than from the address. */
  private routeTemplate(): string {
    const router = this.injector.get(Router, null);
    if (router === null) {
      return '';
    }

    const parts: string[] = [];
    let node: ActivatedRouteSnapshot | null = router.routerState.snapshot.root;
    while (node !== null) {
      const path = node.routeConfig?.path;
      if (path) {
        parts.push(path);
      }
      node = node.firstChild;
    }
    return '/' + parts.join('/');
  }
}
