import { Page, Route } from '@playwright/test';

/**
 * Plays the platform for the clinic app in browser tests (spec 003). The app calls it with
 * cookies, so answers name the calling site and allow credentials; the session itself is a
 * flag in the test, since scripts never see the real cookies anyway.
 */
export const CLINIC = 'http://localhost:4201';
export const TRACE = '4bf92f3577b34da6a3ce929d0e0e4736';

export const cors = {
  'access-control-allow-origin': CLINIC,
  'access-control-allow-credentials': 'true',
  'access-control-allow-headers': 'content-type, traceparent, accept-language, x-scs-request',
  'access-control-allow-methods': 'GET, POST, OPTIONS',
  'access-control-expose-headers': 'X-Trace-Id, Retry-After',
};

export const session = {
  accountId: '0199c0de-0000-7000-8000-000000000001',
  email: 'owner@noor.example',
  clinic: { nameAr: 'عيادة النور', nameEn: 'Al Noor Clinic', state: 'active' },
  expiresAt: '2030-01-01T12:00:00Z',
};

export const support = {
  hoursAr: 'يتابع فريق الدعم الطلبات من السبت إلى الخميس، من ٩:٠٠ ص إلى ٩:٠٠ م بتوقيت القاهرة.',
  hoursEn: 'The support team handles requests Saturday to Thursday, 9:00 AM to 9:00 PM Cairo time.',
  hotline: '19999',
  whatsapp: '201000000000',
  email: 'support@scs.example',
};

const TITLES: Record<string, string> = {
  UNAUTHENTICATED: 'يلزم تسجيل الدخول',
  SESSION_ENDED: 'انتهت الجلسة. الدخول من جديد للمتابعة.',
  ACCESS_EXPIRED: 'يلزم تجديد الجلسة',
};

export interface Answer {
  status: number;
  code?: string;
  json?: unknown;
  headers?: Record<string, string>;
  extra?: Record<string, unknown>;
}

export function problem(status: number, code: string, extra: Record<string, unknown> = {}): Answer {
  return { status, code, extra };
}

export interface StaffPlatform {
  /** Whether a session is alive. */
  signedIn: boolean;
  /** What each address answers next; a function is asked every time. */
  signIn: Answer | (() => Answer);
  code: Answer | (() => Answer);
  resend: Answer | (() => Answer);
  renew: Answer | (() => Answer);
  /** Overrides the answer of `GET /v1/auth/session` while set. */
  sessionAnswer: Answer | null;
  /** Every call made, as `METHOD path`. */
  calls: string[];
  /** Bodies posted to sign-in and code. */
  bodies: Record<string, unknown>[];
  /** Headers of the last changing request. */
  lastHeaders: Record<string, string>;
}

async function fulfil(route: Route, answer: Answer): Promise<void> {
  if (answer.code !== undefined) {
    await route.fulfill({
      status: answer.status,
      headers: { ...cors, 'content-type': 'application/problem+json', ...answer.headers },
      body: JSON.stringify({
        type: `urn:scs:error:${answer.code.toLowerCase().replaceAll('_', '-')}`,
        title: TITLES[answer.code] ?? answer.code,
        status: answer.status,
        code: answer.code,
        traceId: TRACE,
        ...answer.extra,
      }),
    });
  } else if (answer.json !== undefined) {
    await route.fulfill({
      status: answer.status,
      headers: { ...cors, ...answer.headers },
      json: answer.json,
    });
  } else {
    await route.fulfill({ status: answer.status, headers: { ...cors, ...answer.headers } });
  }
}

const sent = { expiresInSeconds: 600, resendAvailableInSeconds: 60, resendsLeft: 3 };

export async function staffPlatform(
  page: Page,
  start: Partial<StaffPlatform> = {},
): Promise<StaffPlatform> {
  const state: StaffPlatform = {
    signedIn: false,
    signIn: { status: 200, json: sent },
    code: { status: 200, json: session },
    resend: { status: 200, json: { ...sent, resendsLeft: 2 } },
    renew: { status: 204 },
    sessionAnswer: null,
    calls: [],
    bodies: [],
    lastHeaders: {},
    ...start,
  };

  const pick = (answer: Answer | (() => Answer)) =>
    typeof answer === 'function' ? answer() : answer;

  const on = async (pattern: string, handler: (route: Route) => Promise<void>) => {
    await page.route(pattern, async (route) => {
      const request = route.request();
      if (request.method() === 'OPTIONS') {
        await route.fulfill({ status: 204, headers: cors });
        return;
      }
      state.calls.push(`${request.method()} ${new URL(request.url()).pathname}`);
      if (request.method() !== 'GET') {
        state.lastHeaders = request.headers();
      }
      await handler(route);
    });
  };

  await on('**/v1/meta/version', (route) =>
    fulfil(route, {
      status: 200,
      json: {
        version: '0.3.0',
        commit: 'a'.repeat(40),
        builtAt: '2026-07-01T10:00:00Z',
        environment: 'local',
      },
    }),
  );
  await on('**/health/ready', (route) =>
    fulfil(route, { status: 200, json: { status: 'Healthy' } }),
  );
  await on('**/v1/meta/support', (route) => fulfil(route, { status: 200, json: support }));

  await on('**/v1/auth/session', (route) =>
    fulfil(
      route,
      state.sessionAnswer ??
        (state.signedIn ? { status: 200, json: session } : problem(401, 'UNAUTHENTICATED')),
    ),
  );

  await on('**/v1/auth/sign-in', async (route) => {
    state.bodies.push(route.request().postDataJSON() as Record<string, unknown>);
    await fulfil(route, pick(state.signIn));
  });

  await on('**/v1/auth/sign-in/code', async (route) => {
    state.bodies.push(route.request().postDataJSON() as Record<string, unknown>);
    const answer = pick(state.code);
    if (answer.status === 200) {
      state.signedIn = true;
    }
    await fulfil(route, answer);
  });

  await on('**/v1/auth/sign-in/code/resend', (route) => fulfil(route, pick(state.resend)));
  await on('**/v1/auth/session/renew', (route) => fulfil(route, pick(state.renew)));

  await on('**/v1/auth/sign-out', async (route) => {
    state.signedIn = false;
    await fulfil(route, { status: 204 });
  });

  return state;
}

/** Fills and submits the sign-in form. */
export async function submitSignIn(
  page: Page,
  email = 'owner@noor.example',
  password = 'Correct-Horse-9!',
): Promise<void> {
  await page.getByTestId('email').fill(email);
  await page.getByTestId('password').fill(password);
  await page.getByTestId('sign-in-submit').click();
}

/** Types a six-digit code into the boxes. */
export async function typeCode(page: Page, code: string): Promise<void> {
  await page.getByTestId('code-box-0').click();
  await page.keyboard.type(code);
}

export async function useEnglish(page: Page): Promise<void> {
  await page.addInitScript(() => localStorage.setItem('scs.language', 'en'));
}
