import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { SCS_APP } from '../app-token';
import { CONFIG_FETCH, RuntimeConfigService } from '../config/runtime-config';
import { apiInterceptor } from '../http/api.interceptor';
import { ScsProblemError } from '../http/problem';
import {
  AuthService,
  sessionGuard,
  sessionInterceptor,
  signedOutGuard,
  temporaryPasswordGuard,
} from './auth.service';

const API = 'http://localhost:5080';

const session = {
  accountId: '0199c0de-0000-7000-8000-000000000001',
  email: 'temp@noor.example',
  clinic: { nameAr: 'عيادة النور', nameEn: 'Al Noor Clinic', state: 'active' },
  expiresAt: '2030-01-01T12:00:00Z',
  mustChangePassword: false,
};

function problem(code: string, status: number, extra: Record<string, unknown> = {}) {
  return { type: 'urn:scs:error:x', title: code, status, code, traceId: 'a'.repeat(32), ...extra };
}

@Component({ template: '' })
class Blank {}

/** Spec 004: asking for a link, using one, and being held by a temporary password. */
describe('AuthService — passwords', () => {
  let auth: AuthService;
  let http: HttpClient;
  let backend: HttpTestingController;
  let router: Router;

  async function setup(): Promise<void> {
    TestBed.configureTestingModule({
      providers: [
        { provide: SCS_APP, useValue: 'clinic' },
        provideHttpClient(withInterceptors([sessionInterceptor, apiInterceptor])),
        provideHttpClientTesting(),
        provideRouter([
          { path: 'sign-in', component: Blank, canActivate: [signedOutGuard] },
          { path: 'set-password', component: Blank },
          { path: 'change-password', component: Blank, canActivate: [temporaryPasswordGuard] },
          { path: '**', component: Blank, canActivate: [sessionGuard] },
        ]),
        {
          provide: CONFIG_FETCH,
          useValue: async () => ({ environment: 'local', apiBaseUrl: API }),
        },
      ],
    });
    await TestBed.inject(RuntimeConfigService).load();
    auth = TestBed.inject(AuthService);
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
  }

  const settle = () => new Promise((resolve) => setTimeout(resolve));

  async function signedIn(mustChangePassword: boolean): Promise<void> {
    const loading = auth.load();
    backend.expectOne(`${API}/v1/auth/session`).flush({ ...session, mustChangePassword });
    await loading;
  }

  afterEach(() => {
    backend.verify();
    TestBed.inject(AuthService).ngOnDestroy();
  });

  it('asks for a reset link with the address and nothing else, and learns nothing back', async () => {
    await setup();

    const asking = auth.requestPasswordReset('owner@noor.example');
    const call = backend.expectOne(`${API}/v1/auth/password/forgot`);
    expect(call.request.body).toEqual({ email: 'owner@noor.example' });
    call.flush(null, { status: 202, statusText: '' });

    expect(await asking).toBeUndefined();
    expect(auth.status()).toBe('unknown');
  });

  it('throws when asking is refused for too many requests, with the wait', async () => {
    await setup();

    const asking = auth.requestPasswordReset('owner@noor.example').catch((error: unknown) => error);
    backend.expectOne(`${API}/v1/auth/password/forgot`).flush(problem('RATE_LIMITED', 429), {
      status: 429,
      statusText: '',
      headers: { 'Retry-After': '42' },
    });

    const error = (await asking) as ScsProblemError;
    expect(error.problem.code).toBe('RATE_LIMITED');
    expect(error.retryAfterSeconds).toBe(42);
  });

  it('sends the token in the body, never in the address, and describes a live link', async () => {
    await setup();

    const looking = auth.inspectLink('tok_en-1');
    const call = backend.expectOne(`${API}/v1/auth/password/link`);
    expect(call.request.body).toEqual({ token: 'tok_en-1' });
    expect(call.request.urlWithParams).not.toContain('tok_en-1');
    call.flush({ kind: 'new-account', email: 'new@shifa.example', clinicNameAr: 'عيادة الشفاء' });

    expect(await looking).toEqual({
      live: true,
      value: { kind: 'new-account', email: 'new@shifa.example', clinicNameAr: 'عيادة الشفاء' },
    });
  });

  it('answers a link that no longer works with its kind instead of throwing', async () => {
    await setup();

    for (const [kind, expected] of [
      ['new-account', 'new-account'],
      ['reset', 'reset'],
      [undefined, 'reset'],
    ] as const) {
      const looking = auth.inspectLink('dead');
      backend
        .expectOne(`${API}/v1/auth/password/link`)
        .flush(problem('LINK_INVALID', 410, { kind }), { status: 410, statusText: '' });
      expect(await looking).toEqual({ live: false, kind: expected });

      const setting = auth.setPasswordWithLink('dead', 'Brand-New-Pass-7!');
      backend
        .expectOne(`${API}/v1/auth/password/link/set`)
        .flush(problem('LINK_INVALID', 410, { kind }), { status: 410, statusText: '' });
      expect(await setting).toEqual({ live: false, kind: expected });
    }
  });

  it('sets a password with a link without signing anyone in', async () => {
    await setup();

    const setting = auth.setPasswordWithLink('tok', 'Brand-New-Pass-7!');
    const call = backend.expectOne(`${API}/v1/auth/password/link/set`);
    expect(call.request.body).toEqual({ token: 'tok', password: 'Brand-New-Pass-7!' });
    call.flush({ kind: 'reset' });

    expect(await setting).toEqual({ live: true, value: 'reset' });
    expect(auth.session()).toBeNull();
    expect(auth.status()).toBe('unknown');
  });

  it('throws a refused password and an outage, so the form can show them', async () => {
    await setup();

    const refused = auth.setPasswordWithLink('tok', 'weak').catch((error: unknown) => error);
    backend
      .expectOne(`${API}/v1/auth/password/link/set`)
      .flush(problem('VALIDATION_FAILED', 400, { errors: { password: ['TOO_SHORT'] } }), {
        status: 400,
        statusText: '',
      });
    expect(((await refused) as ScsProblemError).problem.errors?.['password']).toEqual([
      'TOO_SHORT',
    ]);

    const down = auth.inspectLink('tok').catch((error: unknown) => error);
    backend.expectOne(`${API}/v1/auth/password/link`).error(new ProgressEvent('error'));
    expect(((await down) as ScsProblemError).local).toBe(true);
  });

  it('takes a person holding a temporary password to the one page that replaces it', async () => {
    await setup();
    await signedIn(true);

    await router.navigateByUrl('/patients');
    expect(router.url).toBe('/change-password');

    await router.navigateByUrl('/sign-in');
    expect(router.url).toBe('/change-password');
  });

  it('keeps everyone else away from that page', async () => {
    await setup();
    await signedIn(false);

    await router.navigateByUrl('/change-password');
    expect(router.url).toBe('/');
  });

  it('sends a signed-out person from that page to the sign-in form', async () => {
    await setup();
    const loading = auth.load();
    backend
      .expectOne(`${API}/v1/auth/session`)
      .flush(problem('UNAUTHENTICATED', 401), { status: 401, statusText: '' });
    await loading;

    await router.navigateByUrl('/change-password');
    expect(router.url).toBe('/sign-in');
  });

  it('replaces a temporary password and stays signed in, free to go on', async () => {
    await setup();
    await signedIn(true);
    await router.navigateByUrl('/change-password');

    const changing = auth.changeTemporaryPassword('Brand-New-Pass-7!');
    const call = backend.expectOne(`${API}/v1/auth/password/change`);
    expect(call.request.body).toEqual({ password: 'Brand-New-Pass-7!' });
    expect(call.request.withCredentials).toBe(true);
    call.flush(null, { status: 204, statusText: '' });
    await changing;

    expect(auth.status()).toBe('signed-in');
    expect(auth.session()?.mustChangePassword).toBe(false);
    await router.navigateByUrl('/');
    expect(router.url).toBe('/');
  });

  it('keeps the mark when the new password is refused', async () => {
    await setup();
    await signedIn(true);

    const changing = auth.changeTemporaryPassword('same').catch((error: unknown) => error);
    backend
      .expectOne(`${API}/v1/auth/password/change`)
      .flush(problem('VALIDATION_FAILED', 400, { errors: { password: ['SAME_AS_CURRENT'] } }), {
        status: 400,
        statusText: '',
      });

    expect(((await changing) as ScsProblemError).problem.errors?.['password']).toEqual([
      'SAME_AS_CURRENT',
    ]);
    expect(auth.session()?.mustChangePassword).toBe(true);
  });

  it('follows the platform when any call is refused because the password is temporary', async () => {
    await setup();
    await signedIn(false);
    await router.navigateByUrl('/patients');

    const calling = firstValueFrom(http.get('/v1/anything')).catch((error: unknown) => error);
    backend
      .expectOne(`${API}/v1/anything`)
      .flush(problem('PASSWORD_CHANGE_REQUIRED', 403), { status: 403, statusText: '' });
    expect(((await calling) as ScsProblemError).problem.code).toBe('PASSWORD_CHANGE_REQUIRED');
    await settle();

    expect(router.url).toBe('/change-password');
    expect(auth.session()?.mustChangePassword).toBe(true);
    expect(auth.status()).toBe('signed-in');
  });
});
