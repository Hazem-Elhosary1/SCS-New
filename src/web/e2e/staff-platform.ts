import { readFileSync } from 'node:fs';
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
  'access-control-allow-methods': 'GET, POST, PUT, DELETE, OPTIONS',
  'access-control-expose-headers': 'X-Trace-Id, Retry-After',
};

export const session = {
  accountId: '0199c0de-0000-7000-8000-000000000001',
  email: 'owner@noor.example',
  clinic: { nameAr: 'عيادة النور', nameEn: 'Al Noor Clinic', state: 'active' },
  expiresAt: '2030-01-01T12:00:00Z',
};

/** Every code of the platform's catalogue, in its order (spec 005). */
export const ALL_PERMISSIONS: string[] = (
  JSON.parse(
    readFileSync('projects/core/src/lib/access/permission-catalogue.json', 'utf8'),
  ) as { permissions: { code: string }[] }
).permissions.map((permission) => permission.code);

/** The roles a person in these tests may hold, as the platform describes them. */
export const roles = {
  owner: { id: '0199c0de-0000-7000-8000-0000000000a1', name: 'مالك العيادة', readyKey: 'owner' },
  doctor: { id: '0199c0de-0000-7000-8000-0000000000a3', name: 'طبيب', readyKey: 'doctor' },
  team: { id: '0199c0de-0000-7000-8000-0000000000a4', name: 'فريق العيادة', readyKey: null },
} as const;

export const TEAM_PERMISSIONS = [
  'bookings.view',
  'bookings.manage',
  'bookings.attendance',
  'patients.view',
  'money.view',
  'money.collect',
];

/** What `GET /v1/roles` answers for the seeded Noor clinic. */
export const rolesList = {
  catalogueSize: ALL_PERMISSIONS.length,
  roles: [
    { ...roles.owner, description: 'كل صلاحيات العيادة', category: 'ready', fixed: true, permissionCount: ALL_PERMISSIONS.length, holders: 1 },
    { ...roles.doctor, description: null, category: 'ready', fixed: false, permissionCount: 40, holders: 2 },
    { ...roles.team, description: 'مكتب المواعيد والخزنة في وظيفة واحدة', category: 'custom', fixed: false, permissionCount: 6, holders: 0 },
  ],
};

/** A token as it travels in an emailed link, after the `#t=`. */
export const TOKEN = 'CfDJ8Abc-123_test-token';

export const resetLink = {
  kind: 'reset',
  email: 'owner@noor.example',
  clinicNameAr: null,
  clinicNameEn: null,
};

export const newAccountLink = {
  kind: 'new-account',
  email: 'new@shifa.example',
  clinicNameAr: 'عيادة الشفاء',
  clinicNameEn: 'Al Shifa Clinic',
};

/** The clinic's plan as the session describes it (spec 014, contracts/enforcement-api.md §6). */
export interface PlatformPlan {
  nameAr: string | null;
  nameEn: string | null;
  withheld: string[];
  names: Record<string, { ar: string; en: string }>;
}

export const NO_PLAN: PlatformPlan = { nameAr: null, nameEn: null, withheld: [], names: {} };

/** A plan that leaves out the given features, named as the catalogue names them. */
export function planWithout(names: Record<string, { ar: string; en: string }>): PlatformPlan {
  return { nameAr: 'الباقة الأساسية', nameEn: 'Basic Plan', withheld: Object.keys(names), names };
}

/** How the platform refuses a password: one field code per reason. */
export function passwordRefused(...codes: string[]): Answer {
  return problem(400, 'VALIDATION_FAILED', { errors: { password: codes } });
}

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
  LINK_INVALID: 'انتهت صلاحية الرابط أو استُعمل',
  PERMISSION_REQUIRED: 'هذه الصفحة ليست في دورك',
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

/** A notification as `GET /v1/notifications` lists it (spec 008). */
export interface PlatformNotification {
  id: string;
  kind: string;
  event: string;
  title: string;
  text: string;
  at: string;
  read: boolean;
  link: string | null;
}

/** A file of a record, as the builders' address lists it (spec 009). */
export interface PlatformFile {
  id: string;
  name: string;
  sizeBytes: number;
  type: 'pdf' | 'png' | 'jpeg';
  state: 'available' | 'being-checked' | 'refused' | 'removed' | 'not-kept';
  refusedFor: 'infected' | 'unreadable' | null;
  uploadedAt: string;
  uploadedByAccountId: string;
  restorableUntil: string | null;
  held: boolean;
}

/** How many notifications one page of the made-up platform holds, as the real one does. */
export const NOTIFICATIONS_PAGE = 20;

export interface StaffPlatform {
  /** Whether a session is alive. */
  signedIn: boolean;
  /** What each address answers next; a function is asked every time. */
  signIn: Answer | (() => Answer);
  code: Answer | (() => Answer);
  resend: Answer | (() => Answer);
  renew: Answer | (() => Answer);
  /** Passwords and activation (spec 004). */
  forgot: Answer | (() => Answer);
  link: Answer | (() => Answer);
  linkSet: Answer | (() => Answer);
  change: Answer | (() => Answer);
  /** The signed-in account holds a temporary password. Cleared by a successful change. */
  mustChange: boolean;
  /** The role the signed-in account holds, and the codes it holds (spec 005). Owner of everything by default. */
  role: { id: string; name: string; readyKey: string | null } | null;
  permissions: string[];
  /** What `GET /v1/roles` answers. */
  rolesAnswer: Answer | (() => Answer);
  /** What the person keeps between sessions (spec 006); a saved choice is merged in. */
  preferences: { language: string | null; sidebarFolded: boolean | null; dismissedBanner: string | null };
  /** What `PUT /v1/account/preferences` answers, and every body it was sent. */
  preferencesAnswer: Answer | (() => Answer);
  preferencesSent: Record<string, unknown>[];
  /** Overrides the answer of `GET /v1/auth/session` while set. */
  sessionAnswer: Answer | null;
  /** What the clinic's plan leaves out (spec 014): nothing by default, as while every feature is upcoming. */
  plan: PlatformPlan;
  /** The signed-in person's notifications, newest first (spec 008). None by default. */
  notifications: PlatformNotification[];
  /** Overrides the answer of `GET /v1/notifications` while set. */
  notificationsAnswer: Answer | null;
  /** Every call to the notification addresses, as `METHOD path`, apart from `calls`: the bell asks by itself. */
  notificationCalls: string[];
  /** The files saved on the builders' record (spec 009). None by default. */
  files: PlatformFile[];
  /** What the next uploads answer instead of taking the file; a function is asked for each file by its name. */
  uploadAnswer: Answer | ((name: string) => Answer | null) | null;
  /** How long an upload takes to be answered, so its progress can be seen. */
  uploadDelayMs: number;
  /** What a saved file's scan says when it is saved: available by default. */
  savedAs: PlatformFile['state'];
  /** Overrides the answer of `POST /v1/files/{id}/access` while set. */
  accessAnswer: Answer | null;
  /** Every call to the files addresses, as `METHOD path`. */
  fileCalls: string[];
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
    forgot: { status: 202 },
    link: { status: 200, json: resetLink },
    linkSet: { status: 200, json: { kind: 'reset' } },
    change: { status: 204 },
    mustChange: false,
    role: roles.owner,
    permissions: ALL_PERMISSIONS,
    rolesAnswer: { status: 200, json: rolesList },
    preferences: { language: null, sidebarFolded: null, dismissedBanner: null },
    preferencesAnswer: { status: 204 },
    preferencesSent: [],
    sessionAnswer: null,
    plan: NO_PLAN,
    notifications: [],
    notificationsAnswer: null,
    notificationCalls: [],
    files: [],
    uploadAnswer: null,
    uploadDelayMs: 0,
    savedAs: 'available',
    accessAnswer: null,
    fileCalls: [],
    calls: [],
    bodies: [],
    lastHeaders: {},
    ...start,
  };

  const pick = (answer: Answer | (() => Answer)) =>
    typeof answer === 'function' ? answer() : answer;

  /** The session as the platform describes it now: a temporary password lists no permission. */
  const described = () => ({
    ...session,
    mustChangePassword: state.mustChange,
    role: state.role,
    permissions: state.mustChange ? [] : state.permissions,
    preferences: state.preferences,
    plan: state.plan,
  });

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
        (state.signedIn
          ? { status: 200, json: described() }
          : problem(401, 'UNAUTHENTICATED')),
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
    await fulfil(
      route,
      answer.json === session
        ? { ...answer, json: described() }
        : answer,
    );
  });

  await on('**/v1/auth/sign-in/code/resend', (route) => fulfil(route, pick(state.resend)));
  await on('**/v1/auth/session/renew', (route) => fulfil(route, pick(state.renew)));

  const recorded = (pattern: string, answer: () => Answer, after?: (answer: Answer) => void) =>
    on(pattern, async (route) => {
      state.bodies.push(route.request().postDataJSON() as Record<string, unknown>);
      const given = answer();
      after?.(given);
      await fulfil(route, given);
    });

  await recorded('**/v1/auth/password/forgot', () => pick(state.forgot));
  await recorded('**/v1/auth/password/link', () => pick(state.link));
  await recorded('**/v1/auth/password/link/set', () => pick(state.linkSet));
  await recorded(
    '**/v1/auth/password/change',
    () => pick(state.change),
    (given) => {
      if (given.status === 204) {
        state.mustChange = false;
      }
    },
  );

  await on('**/v1/roles', (route) => fulfil(route, pick(state.rolesAnswer)));

  await on('**/v1/account/preferences', async (route) => {
    const body = route.request().postDataJSON() as Record<string, unknown>;
    const answer = pick(state.preferencesAnswer);
    state.preferencesSent.push(body);
    if (answer.status === 204) {
      state.preferences = { ...state.preferences, ...body };
    }
    await fulfil(route, answer);
  });

  // The bell asks these by itself, every thirty seconds: they are kept out of `calls`.
  await page.route(/\/v1\/notifications(\/[^?]*)?(\?.*)?$/, async (route) => {
    const request = route.request();
    if (request.method() === 'OPTIONS') {
      await route.fulfill({ status: 204, headers: cors });
      return;
    }
    const url = new URL(request.url());
    const path = url.pathname.slice(url.pathname.indexOf('/v1/notifications'));
    state.notificationCalls.push(`${request.method()} ${path}${url.search}`);

    if (!state.signedIn) {
      await fulfil(route, problem(401, 'UNAUTHENTICATED'));
      return;
    }

    const unread = () => state.notifications.some((each) => !each.read);
    if (request.method() === 'GET' && path === '/v1/notifications/state') {
      await fulfil(route, {
        status: 200,
        json: { unread: unread(), latestAt: state.notifications[0]?.at ?? null },
      });
    } else if (request.method() === 'GET' && path === '/v1/notifications') {
      const from = Number(url.searchParams.get('before') ?? '0');
      const to = from + NOTIFICATIONS_PAGE;
      await fulfil(
        route,
        state.notificationsAnswer ?? {
          status: 200,
          json: {
            items: state.notifications.slice(from, to),
            unread: unread(),
            next: to < state.notifications.length ? String(to) : null,
          },
        },
      );
    } else if (request.method() === 'POST' && path === '/v1/notifications/read-all') {
      state.notifications = state.notifications.map((each) => ({ ...each, read: true }));
      await fulfil(route, { status: 204 });
    } else if (request.method() === 'POST' && path.endsWith('/read')) {
      const id = path.split('/')[3];
      const known = state.notifications.some((each) => each.id === id);
      state.notifications = state.notifications.map((each) =>
        each.id === id ? { ...each, read: true } : each,
      );
      await fulfil(route, known ? { status: 204 } : problem(404, 'NOT_FOUND'));
    } else {
      await fulfil(route, problem(404, 'NOT_FOUND'));
    }
  });

  // Files (spec 009): taking one in, the builders' record, and giving one out.
  const uploaded = new Map<string, PlatformFile>();
  let nextFile = 1;
  const typeOf = (name: string): PlatformFile['type'] =>
    /\.png$/i.test(name) ? 'png' : /\.jpe?g$/i.test(name) ? 'jpeg' : 'pdf';

  await page.route(/\/v1\/(dev\/)?files(\/[^?]*)?(\?.*)?$/, async (route) => {
    const request = route.request();
    if (request.method() === 'OPTIONS') {
      await route.fulfill({ status: 204, headers: cors });
      return;
    }
    const url = new URL(request.url());
    const path = url.pathname.slice(url.pathname.indexOf('/v1/'));
    const method = request.method();
    state.fileCalls.push(`${method} ${path}`);
    if (!state.signedIn) {
      await fulfil(route, problem(401, 'UNAUTHENTICATED'));
      return;
    }

    if (method === 'POST' && path === '/v1/files') {
      const body = request.postDataBuffer() ?? Buffer.alloc(0);
      const head = body.subarray(0, 600).toString('utf8');
      const name = /filename="([^"]*)"/.exec(head)?.[1] ?? 'file';
      if (state.uploadDelayMs > 0) {
        await new Promise((resolve) => setTimeout(resolve, state.uploadDelayMs));
      }
      const refused = typeof state.uploadAnswer === 'function' ? state.uploadAnswer(name) : state.uploadAnswer;
      if (refused !== null) {
        await fulfil(route, refused);
        return;
      }
      const file: PlatformFile = {
        id: `0199f11e-0000-7000-8000-${String(nextFile++).padStart(12, '0')}`,
        name,
        sizeBytes: Math.max(1, body.length - 250),
        type: typeOf(name),
        state: 'being-checked',
        refusedFor: null,
        uploadedAt: new Date().toISOString(),
        uploadedByAccountId: session.accountId,
        restorableUntil: null,
        held: false,
      };
      uploaded.set(file.id, file);
      await fulfil(route, {
        status: 201,
        json: { id: file.id, name, sizeBytes: file.sizeBytes, type: file.type, state: 'being-checked' },
      });
    } else if (method === 'GET' && path === '/v1/dev/files/sample') {
      await fulfil(route, { status: 200, json: { files: state.files } });
    } else if (method === 'POST' && path === '/v1/dev/files/sample') {
      const ids = (request.postDataJSON() as { fileIds: string[] }).fileIds;
      for (const id of ids) {
        const file = uploaded.get(id);
        if (file !== undefined) {
          uploaded.delete(id);
          state.files = [...state.files, { ...file, state: state.savedAs }];
        }
      }
      await fulfil(route, { status: 204 });
    } else if (method === 'POST' && /^\/v1\/dev\/files\/sample\/[^/]+\/(remove|restore)$/.test(path)) {
      const [, , , , , id, action] = path.split('/');
      state.files = state.files.map((file) =>
        file.id !== id
          ? file
          : action === 'remove'
            ? { ...file, state: 'removed', restorableUntil: new Date(Date.now() + 30 * 86_400_000).toISOString() }
            : { ...file, state: 'available', restorableUntil: null },
      );
      await fulfil(route, { status: 204 });
    } else if (method === 'POST' && /^\/v1\/files\/[^/]+\/access$/.test(path)) {
      const id = path.split('/')[3];
      const file = state.files.find((each) => each.id === id);
      await fulfil(
        route,
        state.accessAnswer ??
          (file === undefined
            ? problem(404, 'NOT_FOUND')
            : {
                status: 200,
                json: {
                  url: `/v1/files/content/way-in-${id}`,
                  expiresAt: new Date(Date.now() + 300_000).toISOString(),
                  name: file.name,
                  sizeBytes: file.sizeBytes,
                  type: file.type,
                },
              }),
      );
    } else if (method === 'GET' && path.startsWith('/v1/files/content/')) {
      await route.fulfill({
        status: 200,
        headers: { ...cors, 'content-type': 'application/pdf', 'content-disposition': 'attachment' },
        body: '%PDF-1.7 made-up content',
      });
    } else if (method === 'DELETE' && /^\/v1\/files\/[^/]+$/.test(path)) {
      uploaded.delete(path.split('/')[3]);
      await fulfil(route, { status: 204 });
    } else {
      await fulfil(route, problem(404, 'NOT_FOUND'));
    }
  });

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

/** Signs out the way a person does: from the account menu of the top bar. */
export async function signOut(page: Page): Promise<void> {
  await page.getByTestId('account-chip').click();
  await page.getByTestId('sign-out').click();
}

/**
 * Gives the frame's sources sample data before the console starts (see
 * projects/clinic/src/app/dev/sample-sources.ts). The app reads it outside production only.
 */
export async function sampleSources(page: Page, sample: Record<string, unknown>): Promise<void> {
  await page.addInitScript((given) => {
    (window as unknown as { __scsSample: unknown }).__scsSample = given;
  }, sample);
}

export async function useEnglish(page: Page): Promise<void> {
  await page.addInitScript(() => localStorage.setItem('scs.language', 'en'));
}

/**
 * Saves a 1440x900 picture of the page when `SCS_SHOTS` names a folder — for laying every
 * state beside its frame in the design file — and the same state at a phone's width (390x844) in
 * `narrow/`, then returns to 1440. Does nothing in an ordinary run.
 */
export async function pictureFor(page: Page, group: string, language: string, state: string) {
  const folder = process.env['SCS_SHOTS'];
  if (!folder) {
    return;
  }
  const name = `${group}-${language}-${state.replace(/[^a-z0-9]+/gi, '-').toLowerCase()}.png`;
  // The wide picture first, exactly as before; then the phone's, once the page has settled.
  const settle = () =>
    page.evaluate(() =>
      document
        .getAnimations()
        .filter((animation) => animation.effect?.getComputedTiming().endTime !== Infinity)
        .forEach((animation) => animation.finish()),
    );
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.mouse.move(0, 0);
  // Finite animations and transitions (the side bar widening after the resize) end first.
  await page.screenshot({ path: `${folder}/${name}`, animations: 'disabled' });
  await page.setViewportSize({ width: 390, height: 844 });
  await settle();
  await page.screenshot({ path: `${folder}/narrow/${name}`, animations: 'disabled' });
  await page.setViewportSize({ width: 1440, height: 900 });
  await settle();
}
