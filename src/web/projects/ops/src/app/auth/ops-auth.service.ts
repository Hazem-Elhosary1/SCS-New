import { HttpClient, HttpInterceptorFn } from '@angular/common/http';
import { DOCUMENT, DestroyRef, Injectable, Injector, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { LanguageService, ScsProblemError } from 'core';
import { catchError, firstValueFrom, from, switchMap, throwError } from 'rxjs';

/** Who is signed in, as the frame shows them. */
export interface OpsOperator {
  readonly id: string;
  readonly fullName: string;
  readonly language: 'ar' | 'en';
  readonly roleName: string;
}

export interface OpsSession {
  readonly operator: OpsOperator;
  readonly expiresAt: string;
  /** The idle period, for the console's own clock. */
  readonly idleMinutes: number;
  readonly newBrowser?: boolean;
}

/** A code is in the operator's email: where it went and when another may be asked for. */
export interface OpsCodeStep {
  readonly email: string;
  readonly expiresAt: string;
  readonly resendAt: string;
  readonly resendsLeft: number;
  /** The platform's time, so a wrong clock does not spoil the countdown. */
  readonly now: string;
}

/**
 * Why a session ended when the operator did not end it (contracts/operator-sessions.md §3), or
 * what just happened before the sign-in screen is shown. Shown once.
 */
export type OpsNotice =
  | 'replaced'
  | 'idle'
  | 'expired'
  | 'password-reset'
  | 'suspended'
  | 'role-changed'
  | 'reuse'
  | 'locked'
  | 'password-changed'
  | 'account-ready';

export const OpsCodes = {
  accessExpired: 'ACCESS_EXPIRED',
  sessionEnded: 'SESSION_ENDED',
  unauthenticated: 'UNAUTHENTICATED',
  invalidCredentials: 'INVALID_CREDENTIALS',
  accountLocked: 'ACCOUNT_LOCKED',
  accountDisabled: 'ACCOUNT_DISABLED',
  codeInvalid: 'CODE_INVALID',
  stepExpired: 'STEP_EXPIRED',
  codeNotSent: 'SIGN_IN_CODE_NOT_SENT',
  codeNotResent: 'SIGN_IN_CODE_NOT_RESENT',
  resendTooEarly: 'RESEND_TOO_EARLY',
  resendLimit: 'RESEND_LIMIT_REACHED',
  linkInvalid: 'LINK_INVALID',
  rateLimited: 'RATE_LIMITED',
  validation: 'VALIDATION_FAILED',
} as const;

/** The console marks what a page asks by itself: it is not the operator's activity (FR-032). */
export const BACKGROUND_HEADER = 'X-Scs-Background';

const SESSION = '/v1/ops/auth/session';
const RENEW = '/v1/ops/auth/session/renew';

/** How often an open console asks whether its session still stands (FR-030). */
export const SESSION_CHECK_MS = 20_000;

const CHANNEL = 'scs-ops-auth';
const ACTIVITY_EVENTS = ['keydown', 'pointerdown', 'wheel', 'touchstart'] as const;

/** A field the platform added to a problem: a reason, a time, a count. */
export function problemField<T>(error: unknown, name: string): T | null {
  if (!(error instanceof ScsProblemError)) {
    return null;
  }
  const value = (error.problem as unknown as Record<string, unknown>)[name];
  return value === undefined || value === null ? null : (value as T);
}

/**
 * The operator's session in the console (spec 010). It is not the clinic's: its own
 * addresses, its own channel between tabs, and nothing of a clinic. It keeps who is signed in,
 * renews the session in use, signs out when the console is left untouched for the idle
 * period, and learns within half a minute that the session was ended elsewhere.
 */
@Injectable({ providedIn: 'root' })
export class OpsAuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly language = inject(LanguageService);
  private readonly document = inject(DOCUMENT);

  private readonly current = signal<OpsSession | null>(null);
  private readonly settled = signal(false);
  private readonly pending = signal<OpsCodeStep | null>(null);
  private readonly said = signal<OpsNotice | null>(null);

  private renewing: Promise<boolean> | null = null;
  private lastActivity = Date.now();
  private timer: ReturnType<typeof setInterval> | null = null;
  private readonly channel = this.openChannel();
  private readonly onActivity = () => (this.lastActivity = Date.now());
  private readonly onShown = () => {
    if (this.document.visibilityState === 'visible') {
      void this.tick();
    }
  };

  /** The signed-in operator's session; null when nobody is signed in. */
  readonly session = this.current.asReadonly();

  readonly signedIn = computed(() => this.current() !== null);

  /** The code step in progress, between the password and the code. */
  readonly codeStep = this.pending.asReadonly();

  /** What the sign-in screen has to say once (a reason, or good news). */
  readonly notice = this.said.asReadonly();

  constructor() {
    inject(DestroyRef).onDestroy(() => {
      this.stopWatching();
      this.channel?.close();
    });
  }

  /** Asked once when the console opens: is there a session already? */
  async load(): Promise<boolean> {
    if (this.settled()) {
      return this.signedIn();
    }

    try {
      this.accept(await firstValueFrom(this.http.get<OpsSession>(SESSION)));
    } catch (error) {
      this.current.set(null);
      this.keepReason(error);
    }
    this.settled.set(true);
    return this.signedIn();
  }

  /** Step one: email and password. Opens nothing; a code is sent. */
  async signIn(email: string, password: string, remember: boolean): Promise<void> {
    this.said.set(null);
    this.pending.set(
      await firstValueFrom(
        this.http.post<OpsCodeStep>('/v1/ops/auth/sign-in', { email, password, remember }),
      ),
    );
  }

  /** Step two: the code opens the session. */
  async confirm(code: string): Promise<void> {
    const session = await firstValueFrom(
      this.http.post<OpsSession>('/v1/ops/auth/code', { code }),
    );
    this.pending.set(null);
    this.settled.set(true);
    this.accept(session);
    this.channel?.postMessage('signed-in');
  }

  async resend(): Promise<void> {
    this.pending.set(
      await firstValueFrom(this.http.post<OpsCodeStep>('/v1/ops/auth/code/resend', null)),
    );
  }

  /** Leaves the code step: its code stops working. */
  async cancel(): Promise<void> {
    this.pending.set(null);
    await firstValueFrom(this.http.post('/v1/ops/auth/code/cancel', null)).catch(() => undefined);
  }

  /** The code step is over on the platform's side; the sign-in form starts again. */
  forgetCodeStep(): void {
    this.pending.set(null);
  }

  /** Renews the session in use. One renewal at a time, whoever asks. */
  renew(): Promise<boolean> {
    this.renewing ??= firstValueFrom(this.http.post(RENEW, null))
      .then(() => true)
      .catch((error: unknown) => {
        this.keepReason(error);
        return false;
      })
      .finally(() => (this.renewing = null));
    return this.renewing;
  }

  async signOut(): Promise<void> {
    await firstValueFrom(this.http.post('/v1/ops/auth/sign-out', null)).catch(() => undefined);
    this.channel?.postMessage('signed-out');
    await this.leave(null);
  }

  async signOutEverywhere(): Promise<void> {
    await firstValueFrom(this.http.post('/v1/ops/auth/sign-out-everywhere', null));
    this.channel?.postMessage('signed-out');
    await this.leave(null);
  }

  /** The platform said the session is over. The sign-in screen says why, once. */
  async ended(error?: unknown): Promise<void> {
    if (error !== undefined) {
      this.keepReason(error);
    }
    await this.leave(this.said());
  }

  /** Something to say on the sign-in screen: a password was changed, an account is ready. */
  say(notice: OpsNotice): void {
    this.said.set(notice);
  }

  /** The sign-in screen has shown its notice. */
  takeNotice(): OpsNotice | null {
    const notice = this.said();
    this.said.set(null);
    return notice;
  }

  private accept(session: OpsSession): void {
    this.current.set(session);
    // After sign-in the console speaks the account's language (FR-054).
    this.language.use(session.operator.language);
    this.watch();
  }

  private async leave(notice: OpsNotice | null): Promise<void> {
    const wasIn = this.signedIn();
    this.stopWatching();
    this.current.set(null);
    this.pending.set(null);
    this.settled.set(true);
    this.said.set(notice);
    if (wasIn || !this.router.url.startsWith('/sign-in')) {
      await this.router.navigateByUrl('/sign-in');
    }
  }

  private keepReason(error: unknown): void {
    const reason = problemField<OpsNotice>(error, 'reason');
    if (reason !== null) {
      this.said.set(reason);
    }
  }

  // ---------------------------------------------------------------- the idle clock and the check

  private watch(): void {
    if (this.timer !== null) {
      return;
    }
    this.lastActivity = Date.now();
    for (const name of ACTIVITY_EVENTS) {
      this.document.addEventListener(name, this.onActivity, { passive: true, capture: true });
    }
    this.document.addEventListener('visibilitychange', this.onShown);
    this.timer = setInterval(() => void this.tick(), SESSION_CHECK_MS);
  }

  private stopWatching(): void {
    if (this.timer !== null) {
      clearInterval(this.timer);
      this.timer = null;
    }
    for (const name of ACTIVITY_EVENTS) {
      this.document.removeEventListener(name, this.onActivity, { capture: true });
    }
    this.document.removeEventListener('visibilitychange', this.onShown);
  }

  /**
   * Every twenty seconds while the tab is shown: an untouched console signs out at the idle
   * period; otherwise it asks, as a background request, whether its session still stands, so
   * that one ended elsewhere is shown without anybody touching this one (FR-030, FR-031).
   */
  private async tick(): Promise<void> {
    const session = this.current();
    if (session === null || this.document.visibilityState !== 'visible') {
      return;
    }

    if (Date.now() - this.lastActivity >= session.idleMinutes * 60_000) {
      await firstValueFrom(this.http.post('/v1/ops/auth/sign-out', null)).catch(() => undefined);
      await this.leave('idle');
      return;
    }

    try {
      await firstValueFrom(this.http.get(SESSION, { headers: { [BACKGROUND_HEADER]: '1' } }));
    } catch {
      // The interceptor has already shown the sign-in screen when the session was over.
    }
  }

  private openChannel(): BroadcastChannel | null {
    if (typeof BroadcastChannel === 'undefined') {
      return null;
    }
    const channel = new BroadcastChannel(CHANNEL);
    channel.onmessage = (event: MessageEvent<unknown>) => {
      if (event.data === 'signed-out' && this.signedIn()) {
        void this.leave(null);
      } else if (event.data === 'signed-in' && !this.signedIn()) {
        this.settled.set(false);
        void this.load().then((signedIn) => (signedIn ? this.router.navigateByUrl('/') : null));
      }
    };
    return channel;
  }
}

const OWN_STEPS = [
  '/v1/ops/auth/sign-in',
  '/v1/ops/auth/code',
  '/v1/ops/auth/sign-out',
  '/v1/ops/password/',
  RENEW,
];

/**
 * What the console does with the platform's answers about the session: renew when the short
 * access has run out and ask again; show the sign-in screen, with its reason, when the session
 * is over. The addresses of signing in answer for themselves.
 */
export const opsSessionInterceptor: HttpInterceptorFn = (request, next) => {
  if (!request.url.includes('/v1/ops/') || OWN_STEPS.some((path) => request.url.includes(path))) {
    return next(request);
  }

  // Resolved when needed: the service itself makes calls that pass through here.
  const injector = inject(Injector);

  return next(request).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof ScsProblemError)) {
        return throwError(() => error);
      }

      const auth = injector.get(OpsAuthService);

      if (error.problem.code === OpsCodes.accessExpired) {
        return from(auth.renew()).pipe(
          switchMap((renewed) => {
            if (renewed) {
              return next(request);
            }
            void auth.ended();
            return throwError(() => error);
          }),
        );
      }

      if (
        error.problem.code === OpsCodes.sessionEnded ||
        (error.problem.code === OpsCodes.unauthenticated && auth.signedIn())
      ) {
        void auth.ended(error);
      }

      return throwError(() => error);
    }),
  );
};
