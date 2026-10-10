import { HttpClient, HttpInterceptorFn } from '@angular/common/http';
import { Injectable, Injector, OnDestroy, inject, signal } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, firstValueFrom, from, switchMap, throwError } from 'rxjs';
import { SCS_APP } from '../app-token';
import { ScsProblemError } from '../http/problem';

/** The signed-in person, as the platform describes them (contracts/identity-api.openapi.yaml). */
export interface StaffSession {
  readonly accountId: string;
  readonly email: string;
  readonly clinic: { readonly nameAr: string; readonly nameEn: string; readonly state: string };
  readonly expiresAt: string;
}

/** What the platform says after sending a sign-in code. */
export interface CodeSent {
  readonly expiresInSeconds: number;
  readonly resendAvailableInSeconds: number;
  readonly resendsLeft: number;
}

export type SessionStatus = 'unknown' | 'signed-in' | 'signed-out';

export const AuthCodes = {
  unauthenticated: 'UNAUTHENTICATED',
  accessExpired: 'ACCESS_EXPIRED',
  sessionEnded: 'SESSION_ENDED',
  invalidCredentials: 'INVALID_CREDENTIALS',
  codeInvalid: 'CODE_INVALID',
  accountLocked: 'ACCOUNT_LOCKED',
  accountDisabled: 'ACCOUNT_DISABLED',
  clinicSuspended: 'CLINIC_SUSPENDED',
  networkBlocked: 'NETWORK_BLOCKED',
  rateLimited: 'RATE_LIMITED',
  resendTooEarly: 'RESEND_TOO_EARLY',
  resendLimitReached: 'RESEND_LIMIT_REACHED',
  codeNotSent: 'SIGN_IN_CODE_NOT_SENT',
  codeNotResent: 'SIGN_IN_CODE_NOT_RESENT',
} as const;

const CHANNEL = 'scs-auth';

function isSession(value: unknown): value is StaffSession {
  return typeof value === 'object' && value !== null && 'accountId' in value && 'clinic' in value;
}

/**
 * Who is signed in on this browser, and the calls that change it (spec 003). The session
 * itself lives in cookies scripts cannot read; this service only knows what the platform tells
 * it. Nothing here is kept in browser storage: the email typed at the first step is held in
 * memory for the second and is gone on a reload.
 */
@Injectable({ providedIn: 'root' })
export class AuthService implements OnDestroy {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private readonly current = signal<StaffSession | null>(null);
  private readonly known = signal<SessionStatus>('unknown');
  private readonly pending = signal<{ email: string; code: CodeSent } | null>(null);

  private loading: Promise<void> | null = null;
  private renewing: Promise<boolean> | null = null;
  private readonly channel = this.openChannel();

  /** The signed-in person, or null. */
  readonly session = this.current.asReadonly();

  readonly status = this.known.asReadonly();

  /** The sign-in waiting for its code: the email it was sent to and the countdown figures. */
  readonly pendingSignIn = this.pending.asReadonly();

  ngOnDestroy(): void {
    this.channel?.close();
  }

  /** Asks the platform once who is signed in. Safe to call from every guard. */
  load(): Promise<void> {
    this.loading ??= this.refresh();
    return this.loading;
  }

  /**
   * Step one. Resolves to `code` when a code was sent, or `signed-in` when the platform's
   * second step is switched off. Refusals are thrown as ScsProblemError.
   */
  async signIn(email: string, password: string): Promise<'code' | 'signed-in'> {
    const answer = await firstValueFrom(
      this.http.post<CodeSent | StaffSession>('/v1/auth/sign-in', { email, password }),
    );

    if (isSession(answer)) {
      this.entered(answer);
      return 'signed-in';
    }

    this.pending.set({ email: email.trim(), code: answer });
    return 'code';
  }

  /** Step two. On success the person is signed in; refusals are thrown. */
  async confirmCode(code: string): Promise<void> {
    const session = await firstValueFrom(
      this.http.post<StaffSession>('/v1/auth/sign-in/code', { code }),
    );
    this.entered(session);
  }

  /** Sends a new code to the same email and returns the new countdown figures. */
  async resendCode(): Promise<CodeSent> {
    const sent = await firstValueFrom(
      this.http.post<CodeSent>('/v1/auth/sign-in/code/resend', null),
    );
    const pending = this.pending();
    if (pending !== null) {
      this.pending.set({ email: pending.email, code: sent });
    }
    return sent;
  }

  /** Leaves the code step without signing in. */
  abandonSignIn(): void {
    this.pending.set(null);
  }

  /** Ends this browser's session and shows the plain sign-in form. Never fails. */
  async signOut(): Promise<void> {
    try {
      await firstValueFrom(this.http.post('/v1/auth/sign-out', null));
    } catch {
      // Whatever the platform said, this browser no longer shows anything as signed in.
    }
    this.left();
    this.channel?.postMessage('signed-out');
    await this.router.navigateByUrl('/sign-in');
  }

  /**
   * Renews the session. Calls that arrive while a renewal is in flight share it, so three
   * requests that find the access cookie expired cause one renewal, not three.
   */
  renew(): Promise<boolean> {
    this.renewing ??= firstValueFrom(this.http.post('/v1/auth/session/renew', null))
      .then(
        () => true,
        () => false,
      )
      .finally(() => {
        this.renewing = null;
      });
    return this.renewing;
  }

  /**
   * The session was ended by something other than the person signing out: clear what is known
   * and show the sign-in form with the neutral notice. It never says why (FR-041).
   */
  async ended(): Promise<void> {
    if (this.known() === 'signed-out' && this.router.url.startsWith('/sign-in')) {
      return;
    }
    this.left();
    await this.router.navigate(['/sign-in'], { queryParams: { ended: 1 } });
  }

  private async refresh(): Promise<void> {
    try {
      this.entered(await firstValueFrom(this.http.get<StaffSession>('/v1/auth/session')), false);
    } catch {
      this.left();
    }
  }

  private entered(session: StaffSession, announce = true): void {
    this.current.set(session);
    this.known.set('signed-in');
    this.pending.set(null);
    if (announce) {
      this.channel?.postMessage('signed-in');
    }
  }

  private left(): void {
    this.current.set(null);
    this.known.set('signed-out');
    this.pending.set(null);
  }

  /** Other tabs of this browser follow a sign-in or a sign-out at once. */
  private openChannel(): BroadcastChannel | null {
    if (typeof BroadcastChannel === 'undefined') {
      return null;
    }

    const channel = new BroadcastChannel(CHANNEL);
    channel.onmessage = (event: MessageEvent<unknown>) => {
      if (event.data === 'signed-out') {
        this.left();
        void this.router.navigateByUrl('/sign-in');
      } else if (event.data === 'signed-in' && this.known() !== 'signed-in') {
        void this.refresh().then(() => {
          if (this.known() === 'signed-in' && this.router.url.startsWith('/sign-in')) {
            void this.router.navigateByUrl('/');
          }
        });
      }
    };
    return channel;
  }
}

/** Only a signed-in person passes; anyone else is shown the sign-in form. */
export const sessionGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  await auth.load();
  return auth.status() === 'signed-in' ? true : router.parseUrl('/sign-in');
};

/** The sign-in form is for people who are not signed in; the others are taken inside. */
export const signedOutGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  await auth.load();
  return auth.status() === 'signed-in' ? router.parseUrl('/') : true;
};

/** The code step exists only while a sign-in is waiting for its code. */
export const pendingSignInGuard: CanActivateFn = () =>
  inject(AuthService).pendingSignIn() !== null ? true : inject(Router).parseUrl('/sign-in');

const HANDLES_ITS_OWN = ['/v1/auth/sign-in', '/v1/auth/session/renew', '/v1/auth/sign-out'];

/**
 * Keeps a session going without the person noticing, and notices when it is over (spec 003).
 * Place it before the API interceptor. Only the staff and operators sites use it.
 *
 * - `ACCESS_EXPIRED`: renew once, then repeat the call once.
 * - `SESSION_ENDED`, or a renewal that is refused: clear the screen and show the sign-in form
 *   with the "session ended" notice.
 */
export const sessionInterceptor: HttpInterceptorFn = (request, next) => {
  const app = inject(SCS_APP, { optional: true });
  if (app === 'patient' || HANDLES_ITS_OWN.some((path) => request.url.includes(path))) {
    return next(request);
  }

  // Resolved when needed: the service itself makes calls that pass through here.
  const injector = inject(Injector);

  return next(request).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof ScsProblemError)) {
        return throwError(() => error);
      }

      const auth = injector.get(AuthService);

      if (error.problem.code === AuthCodes.accessExpired) {
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

      if (error.problem.code === AuthCodes.sessionEnded) {
        void auth.ended();
      }

      return throwError(() => error);
    }),
  );
};
