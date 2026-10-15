import { Page, Route } from '@playwright/test';

/**
 * Plays the platform for the operators console in browser tests (spec 010). The console calls
 * it with cookies, so answers name the calling site and allow credentials; the session itself
 * is a flag in the test, since scripts never see the real cookies anyway.
 */
export const OPS = 'http://localhost:4203';
const TRACE = '4bf92f3577b34da6a3ce929d0e0e4736';

const cors = {
  'access-control-allow-origin': OPS,
  'access-control-allow-credentials': 'true',
  'access-control-allow-headers':
    'content-type, traceparent, accept-language, x-scs-request, x-scs-background',
  'access-control-allow-methods': 'GET, POST, PUT, DELETE, OPTIONS',
  'access-control-expose-headers': 'X-Trace-Id, Retry-After',
};

export interface OpsAnswer {
  status: number;
  json?: unknown;
  code?: string;
  extra?: Record<string, unknown>;
}

export function opsProblem(status: number, code: string, extra: Record<string, unknown> = {}): OpsAnswer {
  return { status, code, extra };
}

export const opsOperator = {
  id: '0199c0de-0000-7000-8000-0000000000a1',
  fullName: 'أحمد سامي',
  language: 'ar' as 'ar' | 'en',
  roleName: 'مسؤول عام',
};

const now = () => new Date().toISOString();
const inSeconds = (seconds: number) => new Date(Date.now() + seconds * 1000).toISOString();

/** A code step as the platform answers it: the wait before "send again" is a minute. */
export const codeStep = (wait = 60, resendsLeft = 3) => ({
  email: 'ahmed.sami@smartclinicos.com',
  expiresAt: inSeconds(600),
  resendAt: inSeconds(wait),
  resendsLeft,
  now: now(),
});

export const opsProfile = () => ({
  fullName: opsOperator.fullName,
  roleName: opsOperator.roleName,
  email: 'ahmed.sami@smartclinicos.com',
  mobile: '+20 100 000 0000',
  twoFactor: { on: true, locked: true },
  sessions: [
    { device: 'Chrome على Windows', address: '41.233.14.89', current: true, startedAt: now(), endedAt: null },
    {
      device: 'Safari على iPhone',
      address: '41.233.14.89',
      current: false,
      startedAt: new Date(Date.now() - 5 * 3600_000).toISOString(),
      endedAt: new Date(Date.now() - 2 * 3600_000).toISOString(),
    },
    {
      device: 'Chrome على Mac',
      address: '156.197.23.10',
      current: false,
      startedAt: new Date(Date.now() - 3 * 86_400_000).toISOString(),
      endedAt: new Date(Date.now() - 2 * 86_400_000).toISOString(),
    },
  ],
});

export interface OpsPlatform {
  signedIn: boolean;
  /** Said once by `GET session` when nobody is signed in: why the last session ended. */
  endedFor: string | null;
  operator: typeof opsOperator;
  idleMinutes: number;
  signIn: OpsAnswer | (() => OpsAnswer);
  code: OpsAnswer | (() => OpsAnswer);
  resend: OpsAnswer | (() => OpsAnswer);
  forgot: OpsAnswer;
  link: OpsAnswer;
  set: OpsAnswer | (() => OpsAnswer);
  profile: OpsAnswer | (() => OpsAnswer);
  change: OpsAnswer | (() => OpsAnswer);
  changeConfirm: OpsAnswer | (() => OpsAnswer);
  changeResend: OpsAnswer;
  /** Every call, as "METHOD /path". */
  calls: string[];
  /** The JSON bodies sent, newest last. */
  bodies: unknown[];
  /** Calls that carried the background header. */
  background: string[];
  lastHeaders: Record<string, string>;
  /** What the operator's role holds now (spec 011); the session read returns it. */
  permissions?: string[];
}

async function fulfil(route: Route, answer: OpsAnswer): Promise<void> {
  if (answer.code !== undefined) {
    await route.fulfill({
      status: answer.status,
      headers: { ...cors, 'content-type': 'application/problem+json' },
      body: JSON.stringify({
        type: `urn:scs:error:${answer.code.toLowerCase().replaceAll('_', '-')}`,
        title: answer.code,
        status: answer.status,
        code: answer.code,
        traceId: TRACE,
        ...answer.extra,
      }),
    });
  } else if (answer.json !== undefined) {
    await route.fulfill({
      status: answer.status,
      headers: { ...cors, 'content-type': 'application/json' },
      body: JSON.stringify(answer.json),
    });
  } else {
    await route.fulfill({ status: answer.status, headers: cors });
  }
}

export async function opsPlatform(page: Page, start: Partial<OpsPlatform> = {}): Promise<OpsPlatform> {
  const state: OpsPlatform = {
    signedIn: false,
    endedFor: null,
    operator: opsOperator,
    idleMinutes: 30,
    signIn: () => ({ status: 200, json: codeStep() }),
    code: { status: 200 },
    resend: () => ({ status: 200, json: codeStep(60, 2) }),
    forgot: { status: 202 },
    link: { status: 200, json: { kind: 'reset', email: 'ahmed.sami@smartclinicos.com' } },
    set: { status: 204 },
    profile: () => ({ status: 200, json: opsProfile() }),
    change: () => ({ status: 202, json: codeStep() }),
    changeConfirm: { status: 204 },
    changeResend: { status: 200, json: codeStep(60, 2) },
    calls: [],
    bodies: [],
    background: [],
    lastHeaders: {},
    ...start,
  };

  const pick = (answer: OpsAnswer | (() => OpsAnswer)) => (typeof answer === 'function' ? answer() : answer);
  // The role's codes are set by the team's fake (spec 011, `ops-team-platform.ts`); without it
  // the session names none, as before roles existed.
  const described = () => ({
    operator: state.operator,
    expiresAt: '2030-01-01T12:00:00Z',
    idleMinutes: state.idleMinutes,
    permissions: state.permissions ?? [],
  });

  const on = async (pattern: string, handler: (route: Route) => Promise<void>) => {
    await page.route(pattern, async (route) => {
      const request = route.request();
      if (request.method() === 'OPTIONS') {
        await route.fulfill({ status: 204, headers: cors });
        return;
      }
      const call = `${request.method()} ${new URL(request.url()).pathname}`;
      state.calls.push(call);
      if (request.headers()['x-scs-background'] === '1') {
        state.background.push(call);
      }
      if (request.method() !== 'GET') {
        state.lastHeaders = request.headers();
        const body = request.postData();
        if (body !== null && body !== '') {
          state.bodies.push(JSON.parse(body));
        }
      }
      await handler(route);
    });
  };

  await on('**/v1/meta/version', (route) =>
    fulfil(route, {
      status: 200,
      json: { version: '0.10.0', commit: 'a'.repeat(40), builtAt: '2026-07-01T10:00:00Z', environment: 'local' },
    }),
  );

  await on('**/v1/ops/auth/session', async (route) => {
    if (state.signedIn) {
      await fulfil(route, { status: 200, json: described() });
      return;
    }
    const reason = state.endedFor;
    state.endedFor = null;
    await fulfil(
      route,
      reason === null
        ? opsProblem(401, 'UNAUTHENTICATED')
        : opsProblem(401, 'SESSION_ENDED', { reason }),
    );
  });

  await on('**/v1/ops/auth/sign-in', (route) => fulfil(route, pick(state.signIn)));
  await on('**/v1/ops/auth/code', async (route) => {
    const answer = pick(state.code);
    if (answer.status === 200) {
      state.signedIn = true;
      await fulfil(route, { status: 200, json: described() });
    } else {
      await fulfil(route, answer);
    }
  });
  await on('**/v1/ops/auth/code/resend', (route) => fulfil(route, pick(state.resend)));
  await on('**/v1/ops/auth/code/cancel', (route) => fulfil(route, { status: 204 }));
  await on('**/v1/ops/auth/session/renew', (route) =>
    fulfil(route, state.signedIn ? { status: 204 } : opsProblem(401, 'SESSION_ENDED')),
  );
  await on('**/v1/ops/auth/sign-out', async (route) => {
    state.signedIn = false;
    await fulfil(route, { status: 204 });
  });
  await on('**/v1/ops/auth/sign-out-everywhere', async (route) => {
    state.signedIn = false;
    await fulfil(route, { status: 204 });
  });

  await on('**/v1/ops/password/forgot', (route) => fulfil(route, state.forgot));
  await on('**/v1/ops/password/link', (route) => fulfil(route, state.link));
  await on('**/v1/ops/password/set', (route) => fulfil(route, pick(state.set)));

  await on('**/v1/ops/profile', (route) =>
    fulfil(route, state.signedIn ? pick(state.profile) : opsProblem(401, 'UNAUTHENTICATED')),
  );
  await on('**/v1/ops/profile/password', (route) => fulfil(route, pick(state.change)));
  await on('**/v1/ops/profile/password/confirm', (route) => fulfil(route, pick(state.changeConfirm)));
  await on('**/v1/ops/profile/password/resend', (route) => fulfil(route, state.changeResend));
  await on('**/v1/ops/profile/password/cancel', (route) => fulfil(route, { status: 204 }));

  return state;
}

export async function opsEnglish(page: Page): Promise<void> {
  await page.addInitScript(() => localStorage.setItem('scs.language', 'en'));
}

/**
 * What a look at the screen would catch, and a test of words does not: nothing scrolls
 * sideways, in the page or in an open dialog; every control and every line of words is whole
 * inside the window, and inside the dialog when one is open; no words are cut off by their box
 * unless the box is made to shorten them. Returns what is wrong, one line each.
 */
export async function opsLooksWrong(page: Page): Promise<string[]> {
  return page.evaluate(() => {
    const wrong: string[] = [];
    const name = (element: Element) =>
      element.getAttribute('data-testid') ??
      `${element.tagName.toLowerCase()}.${String(element.getAttribute('class') ?? '')} "${(element.textContent ?? '').trim().slice(0, 30)}"`;
    const seen = (element: Element) => {
      const box = element.getBoundingClientRect();
      const style = getComputedStyle(element);
      return box.width > 0 && box.height > 0 && style.visibility !== 'hidden' && style.display !== 'none';
    };

    const root = document.documentElement;
    if (root.scrollWidth > root.clientWidth + 1) {
      wrong.push(`the page scrolls sideways (${root.scrollWidth} > ${root.clientWidth})`);
    }

    const dialog = document.querySelector('dialog[open]');
    const frame = dialog?.getBoundingClientRect() ?? { left: 0, right: root.clientWidth };
    if (dialog && dialog.scrollWidth > dialog.clientWidth + 1) {
      wrong.push(`the dialog scrolls sideways (${dialog.scrollWidth} > ${dialog.clientWidth})`);
    }

    const scope = dialog ?? document.body;
    for (const element of scope.querySelectorAll('input, button, a, h1, h2, h3, p, label, strong, small, li, bdi')) {
      if (!seen(element) || element.closest('.cdk-visually-hidden, .sr-only, [hidden]')) {
        continue;
      }
      const box = element.getBoundingClientRect();
      if (box.left < frame.left - 1 || box.right > frame.right + 1) {
        wrong.push(`${name(element)} is outside its frame (${Math.round(box.left)}..${Math.round(box.right)} in ${Math.round(frame.left)}..${Math.round(frame.right)})`);
      }
      // And inside the room its container leaves it: the nearest one that has padding at its sides.
      for (let around = element.parentElement; around && around !== scope.parentElement; around = around.parentElement) {
        const padded = getComputedStyle(around);
        const left = parseFloat(padded.paddingLeft);
        const right = parseFloat(padded.paddingRight);
        if (left + right === 0 || padded.display === 'inline') {
          continue;
        }
        const room = around.getBoundingClientRect();
        if (box.left < room.left + left - 1 || box.right > room.right - right + 1) {
          wrong.push(`${name(element)} runs into the padding of ${name(around).slice(0, 40)}`);
        }
        break;
      }
      const style = getComputedStyle(element);
      if (
        element instanceof HTMLInputElement &&
        ['text', 'email', 'password'].includes(element.type) &&
        style.textAlign !== 'center'
      ) {
        // What is typed never touches the edge of the box drawn around it.
        let drawn: Element | null = element;
        while (drawn && parseFloat(getComputedStyle(drawn).borderTopWidth) === 0) {
          drawn = drawn.parentElement;
        }
        if (drawn) {
          const edge = drawn.getBoundingClientRect();
          const fromRight = style.textAlign === 'right' || (style.textAlign === 'start' && style.direction === 'rtl');
          const room = fromRight
            ? edge.right - box.right + parseFloat(style.paddingRight)
            : box.left - edge.left + parseFloat(style.paddingLeft);
          if (room < 8) {
            wrong.push(`${name(element)} writes against the edge of its box (${Math.round(room)}px of room)`);
          }
        }
      }
      if (
        element.tagName !== 'INPUT' &&
        style.overflowX !== 'visible' &&
        style.textOverflow !== 'ellipsis' &&
        element.scrollWidth > element.clientWidth + 1
      ) {
        wrong.push(`${name(element)} cuts its words off (${element.scrollWidth} > ${element.clientWidth})`);
      }
    }
    return wrong;
  });
}

/** Saves a 1440x900 picture when `SCS_SHOTS` names a folder; nothing in an ordinary run. */
export async function opsPicture(page: Page, language: string, state: string): Promise<void> {
  const folder = process.env['SCS_SHOTS'];
  if (!folder) {
    return;
  }
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.mouse.move(0, 0);
  await page.screenshot({ path: `${folder}/ops-${language}-${state}.png` });
}
