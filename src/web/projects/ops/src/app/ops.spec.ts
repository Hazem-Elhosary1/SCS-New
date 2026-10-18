import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { LanguageService, ScsProblemError } from 'core';
import {
  BACKGROUND_HEADER,
  OpsAuthService,
  opsSessionInterceptor,
  problemField,
} from './auth/ops-auth.service';
import { browserLanguage, startInBrowserLanguage } from './i18n/first-language';
import { passwordRules } from './password/password.pages';
import { agoLabel } from './profile/profile.page';
import { initialsOf } from 'ui';
import { OPS_SECTIONS } from './shell/sections';

const problem = (status: number, code: string, extra: Record<string, unknown> = {}) =>
  new ScsProblemError({ type: 'x', title: '', status, code, traceId: 't', ...extra }, false);

const session = {
  operator: { id: 'o-1', fullName: 'أحمد سامي', language: 'ar', roleName: 'مسؤول عام' },
  expiresAt: '2030-01-01T00:00:00Z',
  idleMinutes: 30,
};

describe('the small pieces of the operators console', () => {
  it('writes initials from the first two words of a name', () => {
    expect(initialsOf('أحمد سامي')).toBe('أس');
    expect(initialsOf('Ahmed Sami Hassan')).toBe('AS');
    expect(initialsOf('  منى ')).toBe('م');
    expect(initialsOf('')).toBe('');
  });

  it('says how long ago as the design does, in the digits of the language', () => {
    const now = Date.parse('2026-10-12T12:00:00Z');
    expect(agoLabel('2026-10-12T10:00:00Z', now, 'ar')).toBe('منذ ساعتين');
    expect(agoLabel('2026-10-10T12:00:00Z', now, 'ar')).toBe('منذ يومين');
    expect(agoLabel('2026-10-12T10:00:00Z', now, 'en')).toBe('2 hours ago');
    expect(agoLabel('2026-10-12T11:55:00Z', now, 'en')).toBe('5 minutes ago');
  });

  it('checks the four rules of a password', () => {
    const met = (password: string) =>
      passwordRules(password, (key) => key)
        .filter((rule) => rule.met)
        .map((rule) => rule.key);

    expect(met('Operator-Horse-9!')).toEqual(['length', 'cases', 'digit', 'symbol']);
    expect(met('Short-9!x')).toEqual(['cases', 'digit', 'symbol']);
    expect(met('alllowercase')).toEqual(['length']);
  });

  it('reads what the platform added to a problem, and nothing from anything else', () => {
    expect(problemField<string>(problem(401, 'SESSION_ENDED', { reason: 'replaced' }), 'reason')).toBe('replaced');
    expect(problemField<string>(problem(401, 'SESSION_ENDED'), 'reason')).toBeNull();
    expect(problemField<string>(new Error('x'), 'reason')).toBeNull();
  });

  it('starts in the language of the browser only when none was chosen, and in Arabic when it speaks neither', () => {
    expect(browserLanguage(['en-GB', 'ar'])).toBe('en');
    expect(browserLanguage(['fr-FR', 'ar-EG', 'en'])).toBe('ar');
    expect(browserLanguage(['fr-FR', 'de'])).toBeNull();
    expect(browserLanguage([])).toBeNull();

    const kept = new Map<string, string>();
    const storage = { getItem: (key: string) => kept.get(key) ?? null, setItem: (key: string, value: string) => void kept.set(key, value) };

    startInBrowserLanguage(storage, ['fr-FR']);
    expect(kept.get('scs.language')).toBeUndefined();
    startInBrowserLanguage(storage, ['en-US']);
    expect(kept.get('scs.language')).toBe('en');
    // A choice already made is not replaced by the browser's.
    startInBrowserLanguage(storage, ['ar-EG']);
    expect(kept.get('scs.language')).toBe('en');
  });

  it('lists only the sections that exist', () => {
    // Spec 011 adds the team, behind its permission; spec 012 the plans, which open the plans list since spec 013;
    // the home stays open to every operator until spec 022; spec 015 adds the clinics.
    expect(OPS_SECTIONS.map((section) => [section.key, section.permission])).toEqual([
      ['home', undefined],
      ['clinics', 'clinics.view'],
      ['plans', 'billing.view'],
      ['operators', 'operators.view'],
    ]);
  });
});

describe('the operator’s session in the console', () => {
  function start() {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: '**', children: [] }]),
        provideHttpClient(withInterceptors([opsSessionInterceptor])),
        provideHttpClientTesting(),
        // The console's language is its own subject; here it only has to be there.
        { provide: LanguageService, useValue: { use: () => undefined, language: () => 'ar' } },
      ],
    });
    return {
      auth: TestBed.inject(OpsAuthService),
      http: TestBed.inject(HttpClient),
      platform: TestBed.inject(HttpTestingController),
    };
  }

  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('asks once who is signed in, and keeps the reason a session ended', async () => {
    const { auth, platform } = start();

    const loading = auth.load();
    platform
      .expectOne('/v1/ops/auth/session')
      .flush(
        { type: 'x', title: '', status: 401, code: 'SESSION_ENDED', traceId: 't', reason: 'replaced' },
        { status: 401, statusText: 'Unauthorized' },
      );

    // Without the API interceptor of the real app the failure is not yet a problem: no reason.
    expect(await loading).toBe(false);
    expect(await auth.load()).toBe(false);
    expect(auth.signedIn()).toBe(false);
  });

  it('opens the session with the code and says nothing on the way', async () => {
    const { auth, platform } = start();

    const first = auth.signIn('a@b.co', 'Operator-Horse-9!', true);
    const sent = platform.expectOne('/v1/ops/auth/sign-in');
    expect(sent.request.body).toEqual({ email: 'a@b.co', password: 'Operator-Horse-9!', remember: true });
    sent.flush({ email: 'a@b.co', expiresAt: 'x', resendAt: 'x', resendsLeft: 3, now: 'x' });
    await first;
    expect(auth.codeStep()?.email).toBe('a@b.co');

    const second = auth.confirm('427913');
    platform.expectOne('/v1/ops/auth/code').flush(session);
    await second;

    expect(auth.signedIn()).toBe(true);
    expect(auth.codeStep()).toBeNull();
    expect(auth.session()?.operator.fullName).toBe('أحمد سامي');

    // Leaving stops the console's own clock, so nothing is asked after the test.
    const out = auth.signOut();
    platform.expectOne('/v1/ops/auth/sign-out').flush(null);
    await out;
    expect(auth.signedIn()).toBe(false);
  });

  it('renews once however many ask, and shows a notice once', async () => {
    const { auth, platform } = start();

    const both = Promise.all([auth.renew(), auth.renew()]);
    platform.expectOne('/v1/ops/auth/session/renew').flush(null);
    expect(await both).toEqual([true, true]);

    auth.say('password-changed');
    expect(auth.takeNotice()).toBe('password-changed');
    expect(auth.takeNotice()).toBeNull();
  });

  it('marks what the console asks by itself with the background header', () => {
    start();
    expect(BACKGROUND_HEADER).toBe('X-Scs-Background');
  });
});
