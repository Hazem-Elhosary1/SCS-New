import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { SCS_APP, ScsApp } from '../app-token';
import { CONFIG_FETCH, RuntimeConfigService } from '../config/runtime-config';
import { REQUEST_HEADER, apiInterceptor } from '../http/api.interceptor';
import { ScsProblemError } from '../http/problem';
import {
  AuthService,
  pendingSignInGuard,
  sessionGuard,
  sessionInterceptor,
  signedOutGuard,
} from './auth.service';

const API = 'http://localhost:5080';

const session = {
  accountId: '0199c0de-0000-7000-8000-000000000001',
  email: 'owner@noor.example',
  clinic: { nameAr: 'عيادة النور', nameEn: 'Al Noor Clinic', state: 'active' },
  expiresAt: '2030-01-01T12:00:00Z',
};

const sent = { expiresInSeconds: 600, resendAvailableInSeconds: 60, resendsLeft: 3 };

function problem(code: string, status: number) {
  return { type: 'urn:scs:error:x', title: code, status, code, traceId: 'a'.repeat(32) };
}

@Component({ template: '' })
class Blank {}

describe('AuthService', () => {
  let auth: AuthService;
  let http: HttpClient;
  let backend: HttpTestingController;
  let router: Router;

  async function setup(app: ScsApp = 'clinic'): Promise<void> {
    TestBed.configureTestingModule({
      providers: [
        { provide: SCS_APP, useValue: app },
        provideHttpClient(withInterceptors([sessionInterceptor, apiInterceptor])),
        provideHttpClientTesting(),
        provideRouter([
          { path: 'sign-in', component: Blank, canActivate: [signedOutGuard] },
          { path: 'sign-in/code', component: Blank, canActivate: [pendingSignInGuard] },
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

  /** Lets promise continuations run between a request being answered and the next assertion. */
  const settle = () => new Promise((resolve) => setTimeout(resolve));

  afterEach(() => {
    backend.verify();
    TestBed.inject(AuthService).ngOnDestroy();
  });

  it('asks who is signed in once, however often it is asked', async () => {
    await setup();
    const first = auth.load();
    const second = auth.load();

    const call = backend.expectOne(`${API}/v1/auth/session`);
    expect(call.request.withCredentials).toBe(true);
    call.flush(session);
    await Promise.all([first, second]);

    expect(auth.status()).toBe('signed-in');
    expect(auth.session()?.email).toBe('owner@noor.example');
    await auth.load();
  });

  it('is signed out when the platform says nobody is signed in', async () => {
    await setup();
    const loading = auth.load();
    backend
      .expectOne(`${API}/v1/auth/session`)
      .flush(problem('UNAUTHENTICATED', 401), { status: 401, statusText: '' });
    await loading;

    expect(auth.status()).toBe('signed-out');
    expect(auth.session()).toBeNull();
  });

  it('keeps the email of step one in memory for step two, and nowhere else', async () => {
    await setup();
    const storageBefore = JSON.stringify({ ...localStorage, ...sessionStorage });

    const signingIn = auth.signIn('  owner@noor.example ', 'Correct-Horse-9!');
    const call = backend.expectOne(`${API}/v1/auth/sign-in`);
    expect(call.request.body).toEqual({
      email: '  owner@noor.example ',
      password: 'Correct-Horse-9!',
    });
    expect(call.request.headers.get(REQUEST_HEADER)).toBe('1');
    expect(call.request.withCredentials).toBe(true);
    call.flush(sent);

    expect(await signingIn).toBe('code');
    expect(auth.pendingSignIn()).toEqual({ email: 'owner@noor.example', code: sent });
    expect(auth.status()).not.toBe('signed-in');
    expect(JSON.stringify({ ...localStorage, ...sessionStorage })).toBe(storageBefore);
  });

  it('is signed in after the code, and the pending sign-in is forgotten', async () => {
    await setup();
    const signingIn = auth.signIn('owner@noor.example', 'x');
    backend.expectOne(`${API}/v1/auth/sign-in`).flush(sent);
    await signingIn;

    const confirming = auth.confirmCode('123456');
    const call = backend.expectOne(`${API}/v1/auth/sign-in/code`);
    expect(call.request.body).toEqual({ code: '123456' });
    call.flush(session);
    await confirming;

    expect(auth.status()).toBe('signed-in');
    expect(auth.pendingSignIn()).toBeNull();
  });

  it('is signed in straight away when the platform has the code step switched off', async () => {
    await setup();
    const signingIn = auth.signIn('owner@noor.example', 'x');
    backend.expectOne(`${API}/v1/auth/sign-in`).flush(session);

    expect(await signingIn).toBe('signed-in');
    expect(auth.status()).toBe('signed-in');
  });

  it('passes a refusal on as a problem and stays where it was', async () => {
    await setup();
    const signingIn = auth.signIn('owner@noor.example', 'wrong');
    backend
      .expectOne(`${API}/v1/auth/sign-in`)
      .flush(problem('INVALID_CREDENTIALS', 401), { status: 401, statusText: '' });

    await expect(signingIn).rejects.toBeInstanceOf(ScsProblemError);
    expect(auth.pendingSignIn()).toBeNull();
  });

  it('updates the countdown figures after a resend', async () => {
    await setup();
    const signingIn = auth.signIn('owner@noor.example', 'x');
    backend.expectOne(`${API}/v1/auth/sign-in`).flush(sent);
    await signingIn;

    const resending = auth.resendCode();
    backend.expectOne(`${API}/v1/auth/sign-in/code/resend`).flush({ ...sent, resendsLeft: 2 });
    await resending;

    expect(auth.pendingSignIn()?.code.resendsLeft).toBe(2);
  });

  it('signs out even when the platform cannot be reached, and shows the plain form', async () => {
    await setup();
    const loading = auth.load();
    backend.expectOne(`${API}/v1/auth/session`).flush(session);
    await loading;

    const leaving = auth.signOut();
    backend.expectOne(`${API}/v1/auth/sign-out`).error(new ProgressEvent('error'));
    await leaving;

    expect(auth.status()).toBe('signed-out');
    expect(router.url).toBe('/sign-in');
  });

  it('renews once for calls that find the access expired together, then repeats each once', async () => {
    await setup();
    const answers: unknown[] = [];
    for (const path of ['/v1/a', '/v1/b', '/v1/c']) {
      http.get(path).subscribe((answer) => answers.push(answer));
    }

    for (const path of ['/v1/a', '/v1/b', '/v1/c']) {
      backend
        .expectOne(`${API}${path}`)
        .flush(problem('ACCESS_EXPIRED', 401), { status: 401, statusText: '' });
    }
    await settle();

    backend.expectOne(`${API}/v1/auth/session/renew`).flush(null, { status: 204, statusText: '' });
    await settle();

    for (const path of ['/v1/a', '/v1/b', '/v1/c']) {
      backend.expectOne(`${API}${path}`).flush({ ok: path });
    }

    expect(answers).toEqual([{ ok: '/v1/a' }, { ok: '/v1/b' }, { ok: '/v1/c' }]);
  });

  it('shows the form with the "session ended" notice when a renewal is refused', async () => {
    await setup();
    let failure: unknown = null;
    http.get('/v1/a').subscribe({ error: (error: unknown) => (failure = error) });

    backend
      .expectOne(`${API}/v1/a`)
      .flush(problem('ACCESS_EXPIRED', 401), { status: 401, statusText: '' });
    await settle();
    backend
      .expectOne(`${API}/v1/auth/session/renew`)
      .flush(problem('SESSION_ENDED', 401), { status: 401, statusText: '' });
    await settle();

    // The form's own guard asks who is signed in; the answer is nobody.
    backend
      .match(`${API}/v1/auth/session`)
      .forEach((call) =>
        call.flush(problem('UNAUTHENTICATED', 401), { status: 401, statusText: '' }),
      );
    await settle();

    expect(failure).toBeInstanceOf(ScsProblemError);
    expect(auth.status()).toBe('signed-out');
    expect(router.url).toBe('/sign-in?ended=1');
  });

  it('shows the same notice when any call says the session has ended', async () => {
    await setup();
    http.get('/v1/a').subscribe({ error: () => undefined });

    backend
      .expectOne(`${API}/v1/a`)
      .flush(problem('SESSION_ENDED', 401), { status: 401, statusText: '' });
    await settle();
    backend
      .match(`${API}/v1/auth/session`)
      .forEach((call) =>
        call.flush(problem('UNAUTHENTICATED', 401), { status: 401, statusText: '' }),
      );
    await settle();

    expect(router.url).toBe('/sign-in?ended=1');
  });

  it('never renews for the sign-in calls themselves', async () => {
    await setup();
    const signingIn = auth.signIn('owner@noor.example', 'x').catch((error: unknown) => error);
    backend
      .expectOne(`${API}/v1/auth/sign-in`)
      .flush(problem('ACCESS_EXPIRED', 401), { status: 401, statusText: '' });

    expect(await signingIn).toBeInstanceOf(ScsProblemError);
    backend.expectNone(`${API}/v1/auth/session/renew`);
  });

  it('sends no cookies and no request header from the patient site', async () => {
    await setup('patient');
    http.post('/v1/anything', {}).subscribe({ error: () => undefined });

    const call = backend.expectOne(`${API}/v1/anything`);
    expect(call.request.withCredentials).toBe(false);
    expect(call.request.headers.has(REQUEST_HEADER)).toBe(false);
    call.flush(problem('ACCESS_EXPIRED', 401), { status: 401, statusText: '' });
    await settle();
    backend.expectNone(`${API}/v1/auth/session/renew`);
  });

  it('sends a signed-out visitor to the form and a signed-in one past it', async () => {
    await setup();
    const going = router.navigateByUrl('/anything');
    await settle();
    backend
      .expectOne(`${API}/v1/auth/session`)
      .flush(problem('UNAUTHENTICATED', 401), { status: 401, statusText: '' });
    await going;
    expect(router.url).toBe('/sign-in');

    // The code step exists only while a sign-in is pending.
    await router.navigateByUrl('/sign-in/code');
    expect(router.url).toBe('/sign-in');

    const signingIn = auth.signIn('owner@noor.example', 'x');
    backend.expectOne(`${API}/v1/auth/sign-in`).flush(session);
    await signingIn;

    await router.navigateByUrl('/somewhere');
    expect(router.url).toBe('/somewhere');

    await router.navigateByUrl('/sign-in');
    await settle();
    expect(router.url).toBe('/');
  });
});
