import { readFileSync } from 'node:fs';
import { Page, Route } from '@playwright/test';
import { OPS, OpsPlatform, opsOperator } from './ops-platform';

/**
 * Plays the team's addresses of the platform for the operators console (spec 011;
 * contracts/operators-roles-api.md): operators and roles held in memory, with the refusals the
 * real platform gives. The rules are the contract's, written again here on purpose — a browser
 * test checks what the console does with an answer, and the answers themselves are proved by
 * the platform's own tests.
 */
const catalogue = JSON.parse(
  readFileSync('projects/ops/src/app/access/operator-permissions.json', 'utf8'),
) as { permissions: { code: string; group: string; needs: string | null }[]; groups: { key: string }[] };

/** Opens a drop-down of the library and chooses the row with this value. */
export async function choose(page: Page, testId: string, value: string): Promise<void> {
  await page.getByTestId(testId).click();
  await page.getByTestId(`${testId}-panel`).locator(`[role="option"][data-value="${value}"]`).click();
}

/** The rows of a drop-down of the library; it is opened first, and stays open. */
export async function rowsOf(page: Page, testId: string) {
  if ((await page.getByTestId(`${testId}-panel`).count()) === 0) {
    await page.getByTestId(testId).click();
  }
  return page.getByTestId(`${testId}-panel`).getByRole('option');
}

export const ALL_CODES = catalogue.permissions.map((permission) => permission.code);
const NEEDS = new Map(catalogue.permissions.map((permission) => [permission.code, permission.needs]));
const ordered = (codes: Iterable<string>) => ALL_CODES.filter((code) => new Set(codes).has(code));
const withNeeded = (codes: readonly string[]) =>
  ordered([...codes, ...codes.map((code) => NEEDS.get(code)).filter((code): code is string => !!code)]);

export const SUPER = '0b11a000-0000-7000-8000-000000000001';
export const BILLING = '0b11a000-0000-7000-8000-000000000002';
export const VIEWER = '0b11a000-0000-7000-8000-000000000003';
export const SUPPORT = '0199c0de-0000-7000-8000-00000000r001';
export const SUCCESS = '0199c0de-0000-7000-8000-00000000r002';

export const VIEWER_CODES = [
  'dashboard.view', 'clinics.view', 'tickets.view', 'tickets.viewAll', 'billing.view', 'operators.view',
  'reports.financial', 'reports.security', 'reports.operators', 'reports.activity', 'settings.view',
];
const BILLING_CODES = [
  'dashboard.view', 'clinics.view', 'clinics.payments', 'clinics.topups', 'tickets.view', 'tickets.reply',
  'billing.view', 'billing.managePlans', 'reports.financial', 'reports.export', 'settings.view',
];

const SYSTEM_NAMES: Record<string, string> = { 'super-admin': 'مسؤول عام', 'billing-admin': 'مسؤول الفوترة', viewer: 'مشاهد فقط' };

export interface FakeRole {
  id: string;
  name: string;
  systemKey: string | null;
  description: string | null;
  codes: string[];
  version: string;
}

export interface FakeOperator {
  id: string;
  fullName: string;
  email: string;
  roleId: string;
  state: 'active' | 'invited' | 'suspended';
  lastSignInAtUtc: string | null;
  twoStep: boolean;
  mobile: string | null;
  department: string | null;
  jobTitle: string | null;
  language: 'ar' | 'en';
  createdAtUtc: string;
  invitedBy: { id: string; name: string } | null;
  locked: boolean;
  version: string;
}

const ago = (minutes: number) => new Date(Date.now() - minutes * 60_000).toISOString();

function roles(): FakeRole[] {
  return [
    { id: SUPER, name: '', systemKey: 'super-admin', description: null, codes: [...ALL_CODES], version: 'r1' },
    { id: BILLING, name: '', systemKey: 'billing-admin', description: null, codes: BILLING_CODES, version: 'r1' },
    { id: VIEWER, name: '', systemKey: 'viewer', description: null, codes: VIEWER_CODES, version: 'r1' },
    {
      id: SUPPORT,
      name: 'وكيل دعم فني',
      systemKey: null,
      description: 'يرد على تذاكر العيادات ويتابعها.',
      codes: ['clinics.view', 'tickets.view', 'tickets.reply', 'tickets.close', 'tickets.create'],
      version: 'r1',
    },
    {
      id: SUCCESS,
      name: 'نجاح العملاء',
      systemKey: null,
      description: 'يتابع نشاط العيادات.',
      codes: ['dashboard.view', 'clinics.view', 'clinics.edit', 'tickets.view', 'tickets.viewAll', 'reports.activity'],
      version: 'r1',
    },
  ];
}

function operators(): FakeOperator[] {
  const person = (index: number, fullName: string, email: string, roleId: string, extra: Partial<FakeOperator> = {}): FakeOperator => ({
    id: `0199c0de-0000-7000-8000-0000000000${(index + 0xa0).toString(16)}`,
    fullName,
    email,
    roleId,
    state: 'active',
    lastSignInAtUtc: ago(5),
    twoStep: true,
    mobile: null,
    department: null,
    jobTitle: null,
    language: 'ar',
    createdAtUtc: new Date(Date.UTC(2026, 0, 12 - index, 9)).toISOString(),
    invitedBy: index === 1 ? null : { id: opsOperator.id, name: opsOperator.fullName },
    locked: false,
    version: 'v1',
    ...extra,
  });

  return [
    person(1, 'أحمد سامي', 'ahmed.sami@smartclinicos.com', SUPER, {
      mobile: '+20 100 111 2233', department: 'management', jobTitle: 'مدير المنصة',
    }),
    person(2, 'سارة أحمد', 'sara.ahmed@smartclinicos.com', SUPPORT, { lastSignInAtUtc: ago(12), department: 'support' }),
    person(3, 'كريم عادل', 'karim.adel@smartclinicos.com', SUPPORT, { lastSignInAtUtc: ago(60 * 26), twoStep: true }),
    person(4, 'مى يوسف', 'mai.youssef@smartclinicos.com', BILLING, { lastSignInAtUtc: ago(60), department: 'billing' }),
    person(5, 'محمد علي', 'mohamed.ali@smartclinicos.com', SUPPORT, { lastSignInAtUtc: ago(120) }),
    person(6, 'ليلى حسن', 'laila.hassan@smartclinicos.com', SUPPORT, { lastSignInAtUtc: ago(60 * 6) }),
    person(7, 'عمر خالد', 'omar.khaled@smartclinicos.com', SUPPORT, { lastSignInAtUtc: ago(60 * 48) }),
    person(8, 'منى إبراهيم', 'mona.ibrahim@smartclinicos.com', VIEWER, { state: 'suspended', lastSignInAtUtc: ago(60 * 72) }),
    person(9, 'Nour Khaled', 'nour.khaled@smartclinicos.com', SUCCESS, { state: 'invited', lastSignInAtUtc: null, language: 'en' }),
    person(10, 'هدى مصطفى', 'hoda.mostafa@smartclinicos.com', SUPPORT, { lastSignInAtUtc: ago(60 * 24 * 9) }),
    person(11, 'يوسف نبيل', 'youssef.nabil@smartclinicos.com', SUPPORT, { lastSignInAtUtc: ago(60 * 24 * 40), locked: true }),
    person(12, 'رنا سمير', 'rana.samir@smartclinicos.com', SUPER, { lastSignInAtUtc: ago(200) }),
  ];
}

export interface OpsTeam {
  roles: FakeRole[];
  operators: FakeOperator[];
  /** The role the signed-in operator holds: decides what the session and every address answer. */
  myRoleId: string;
  /** Makes an address fail: `GET /v1/ops/operators` → a status. */
  failing: Record<string, number>;
  /** Milliseconds every team address waits, to look at the loading states. */
  delay: number;
  /** What `password-link` answers besides success. */
  link: 'ok' | 'too-soon' | 'not-sent';
  /** Sets the role and, with it, what the console is told it may do. */
  actAs(roleId: string): void;
}

const problem = (status: number, code: string, extra: Record<string, unknown> = {}) => ({
  status,
  headers: {
    'access-control-allow-origin': OPS,
    'access-control-allow-credentials': 'true',
    'content-type': 'application/problem+json',
  },
  body: JSON.stringify({ type: `urn:scs:error:${code.toLowerCase()}`, title: code, status, code, traceId: '4bf92f3577b34da6a3ce929d0e0e4736', ...extra }),
});

const ok = (json?: unknown, status = json === undefined ? 204 : 200) => ({
  status,
  headers: {
    'access-control-allow-origin': OPS,
    'access-control-allow-credentials': 'true',
    ...(json === undefined ? {} : { 'content-type': 'application/json' }),
  },
  ...(json === undefined ? {} : { body: JSON.stringify(json) }),
});

/** Adds the team's addresses to a fake platform. Call after `opsPlatform`, which it builds on. */
export async function opsTeam(page: Page, platform: OpsPlatform, start: Partial<OpsTeam> = {}): Promise<OpsTeam> {
  const team: OpsTeam = {
    roles: roles(),
    operators: operators(),
    myRoleId: SUPER,
    failing: {},
    delay: 0,
    link: 'ok',
    actAs(roleId: string) {
      team.myRoleId = roleId;
      const role = roleOf(roleId);
      platform.operator = {
        ...platform.operator,
        roleName: role.systemKey === null ? role.name : SYSTEM_NAMES[role.systemKey],
        role: { id: role.id, name: role.name, systemKey: role.systemKey },
      } as typeof platform.operator;
      platform.permissions = [...role.codes];
    },
    ...start,
  };

  const roleOf = (id: string) => team.roles.find((role) => role.id === id)!;
  const ref = (id: string) => {
    const role = roleOf(id);
    return { id: role.id, name: role.name, systemKey: role.systemKey };
  };
  const mine = () => roleOf(team.myRoleId);
  const superAdmin = () => mine().systemKey === 'super-admin';
  const holds = (code: string) => mine().codes.includes(code);
  const mayGrant = (role: FakeRole) =>
    superAdmin() ? null : role.systemKey === 'super-admin' ? 'super-admin-only' : role.codes.every(holds) ? null : 'holds-more';
  const bump = (version: string) => `${version}+`;
  const row = (person: FakeOperator) => ({
    id: person.id,
    fullName: person.fullName,
    email: person.email,
    role: ref(person.roleId),
    lastSignInAtUtc: person.lastSignInAtUtc,
    state: person.state,
    twoStep: person.twoStep,
  });
  const activeSupers = (except: string) =>
    team.operators.filter((person) => person.id !== except && person.roleId === SUPER && person.state === 'active').length;

  team.actAs(team.myRoleId);

  const handle = async (route: Route): Promise<void> => {
    const request = route.request();
    const url = new URL(request.url());
    const method = request.method();
    if (method === 'OPTIONS') {
      await route.fulfill({
        status: 204,
        headers: {
          'access-control-allow-origin': OPS,
          'access-control-allow-credentials': 'true',
          'access-control-allow-headers': 'content-type, traceparent, accept-language, x-scs-request, x-scs-background',
          'access-control-allow-methods': 'GET, POST, PUT, DELETE, OPTIONS',
        },
      });
      return;
    }

    const path = url.pathname.replace('/v1/ops', '');
    const call = `${method} ${url.pathname}`;
    platform.calls.push(call);
    const body = (request.postData() ? JSON.parse(request.postData()!) : {}) as Record<string, unknown>;
    if (method !== 'GET') {
      platform.bodies.push(body);
    }
    if (team.delay > 0) {
      await new Promise((resolve) => setTimeout(resolve, team.delay));
    }
    const answer = (response: ReturnType<typeof ok>) => route.fulfill(response);
    const need = (code: string) => (holds(code) ? null : problem(403, 'PERMISSION_REQUIRED', { permission: code }));

    if (!platform.signedIn) {
      return answer(problem(401, 'UNAUTHENTICATED'));
    }
    if (team.failing[call] !== undefined) {
      return answer(problem(team.failing[call], 'INTERNAL_ERROR'));
    }

    const parts = path.split('/').filter(Boolean);

    // ---------------------------------------------------------------- roles
    if (parts[0] === 'permissions') {
      return answer(
        need('operators.manageRoles') ??
          ok({
            groups: catalogue.groups.map((group) => ({
              key: group.key,
              items: catalogue.permissions
                .filter((permission) => permission.group === group.key)
                .map((permission) => ({ code: permission.code, needs: permission.needs, mayGrant: superAdmin() || holds(permission.code) })),
            })),
          }),
      );
    }

    if (parts[0] === 'roles') {
      const manages = holds('operators.manageRoles');
      if (parts.length === 1 && method === 'GET') {
        const items = team.roles.map((role) => {
          const holders = team.operators.filter((person) => person.roleId === role.id).length;
          return {
            id: role.id, name: role.name, systemKey: role.systemKey, description: role.description,
            operators: holders, inUse: holders > 0, permissions: role.codes.length,
            can: { edit: manages && role.systemKey === null, delete: manages && role.systemKey === null },
          };
        });
        return answer(
          need('operators.view') ??
            ok({
              items,
              cards: {
                total: items.length, inUse: items.filter((item) => item.inUse).length,
                custom: items.filter((item) => item.systemKey === null).length, system: 3,
              },
            }),
        );
      }
      if (parts[1] === 'options') {
        return answer(
          need('operators.manage') ??
            ok({
              items: team.roles.map((role) => ({ ...ref(role.id), codes: role.codes, grantable: mayGrant(role) === null, reason: mayGrant(role) })),
            }),
        );
      }

      const refused = need('operators.manageRoles');
      if (refused) {
        return answer(refused);
      }
      const taken = (name: string, except: string | null) =>
        ['مسؤول عام', 'super admin', 'مسؤول الفوترة', 'billing manager', 'مشاهد فقط', 'viewer only'].includes(name.trim().toLowerCase()) ||
        team.roles.some((role) => role.id !== except && role.name.trim().toLowerCase() === name.trim().toLowerCase());
      const notHeld = (had: readonly string[], wanted: readonly string[]) =>
        superAdmin() ? [] : withNeeded(wanted).filter((code) => !had.includes(code) && !holds(code));

      if (parts.length === 1 && method === 'POST') {
        const codes = (body['codes'] as string[] | undefined) ?? [];
        const name = String(body['name'] ?? '').trim();
        if (name === '' || codes.length === 0) {
          return answer(problem(400, 'VALIDATION_FAILED', { errors: name === '' ? { name: ['REQUIRED'] } : { codes: ['REQUIRED'] } }));
        }
        if (notHeld([], codes).length > 0) {
          return answer(problem(403, 'OPS_PERMISSIONS_NOT_HELD', { permissions: notHeld([], codes) }));
        }
        if (taken(name, null)) {
          return answer(problem(409, 'OPS_ROLE_NAME_TAKEN'));
        }
        const id = `0199c0de-0000-7000-8000-00000000r${String(team.roles.length + 1).padStart(3, '0')}`;
        team.roles.push({ id, name, systemKey: null, description: (body['description'] as string | null) ?? null, codes: withNeeded(codes), version: 'r1' });
        return answer(ok({ id }, 201));
      }

      const role = team.roles.find((known) => known.id === parts[1]);
      if (!role) {
        return answer(problem(404, 'NOT_FOUND'));
      }
      if (method === 'GET') {
        const kept = role.codes.filter((code) => (superAdmin() || holds(code)) && (!NEEDS.get(code) || superAdmin() || holds(NEEDS.get(code)!)));
        return answer(
          ok({
            id: role.id, name: role.name, description: role.description, systemKey: role.systemKey, codes: role.codes, version: role.version,
            readOnlyReason: role.systemKey !== null ? 'system' : !superAdmin() && role.id === team.myRoleId ? 'own-role' : null,
            copy: { codes: kept, left: role.codes.filter((code) => !kept.includes(code)) },
          }),
        );
      }
      if (role.systemKey !== null) {
        return answer(problem(403, 'OPS_SYSTEM_ROLE'));
      }
      if (parts[2] === 'delete') {
        const holders = team.operators.filter((person) => person.roleId === role.id).length;
        if (holders > 0) {
          return answer(problem(409, 'OPS_ROLE_IN_USE', { operators: holders }));
        }
        team.roles = team.roles.filter((known) => known.id !== role.id);
        return answer(ok());
      }
      if (!superAdmin() && role.id === team.myRoleId) {
        return answer(problem(403, 'OPS_OWN_ROLE_EDIT'));
      }
      if (body['version'] !== role.version) {
        return answer(problem(409, 'OPS_CHANGED_ELSEWHERE'));
      }
      const codes = (body['codes'] as string[] | undefined) ?? [];
      if (notHeld(role.codes, codes).length > 0) {
        return answer(problem(403, 'OPS_PERMISSIONS_NOT_HELD', { permissions: notHeld(role.codes, codes) }));
      }
      if (taken(String(body['name'] ?? ''), role.id)) {
        return answer(problem(409, 'OPS_ROLE_NAME_TAKEN'));
      }
      Object.assign(role, {
        name: String(body['name']).trim(), description: (body['description'] as string | null) ?? null,
        codes: withNeeded(codes), version: bump(role.version),
      });
      if (role.id === team.myRoleId) {
        team.actAs(role.id);
      }
      return answer(ok());
    }

    // ---------------------------------------------------------------- operators
    if (parts[0] !== 'operators') {
      return route.fallback();
    }

    if (parts.length === 1 && method === 'GET') {
      const search = (url.searchParams.get('search') ?? '').trim().toLowerCase();
      const state = url.searchParams.get('state') ?? '';
      const roleId = url.searchParams.get('roleId') ?? '';
      const matching = team.operators
        .filter((person) => search === '' || person.fullName.toLowerCase().includes(search) || person.email.includes(search))
        .filter((person) => state === '' || person.state === state)
        .filter((person) => roleId === '' || person.roleId === roleId);
      const pages = Math.max(1, Math.ceil(matching.length / 8));
      const page = Math.min(Math.max(1, Number(url.searchParams.get('page') ?? '1')), pages);
      const count = (of: string) => team.operators.filter((person) => person.state === of).length;
      return answer(
        need('operators.view') ??
          ok({
            items: matching.slice((page - 1) * 8, page * 8).map(row),
            total: matching.length,
            page,
            pageSize: 8,
            cards: {
              total: team.operators.length, active: count('active'), activatedThisMonth: 2, invited: count('invited'),
              twoStepOn: team.operators.filter((person) => person.twoStep).length,
            },
            filters: {
              states: (['active', 'invited', 'suspended'] as const).filter((of) => count(of) > 0),
              roles: team.roles.filter((role) => team.operators.some((person) => person.roleId === role.id)).map((role) => ref(role.id)),
            },
          }),
      );
    }

    if (parts.length === 1 && method === 'POST') {
      const refused = need('operators.manage');
      if (refused) {
        return answer(refused);
      }
      const email = String(body['email'] ?? '').trim().toLowerCase();
      if (team.operators.some((person) => person.email === email)) {
        return answer(problem(409, 'OPS_EMAIL_TAKEN'));
      }
      const role = team.roles.find((known) => known.id === body['roleId']);
      if (!role) {
        return answer(problem(400, 'VALIDATION_FAILED', { errors: { roleId: ['INVALID'] } }));
      }
      if (mayGrant(role) !== null) {
        return answer(problem(403, 'OPS_ROLE_NOT_GRANTABLE'));
      }
      const id = `0199c0de-0000-7000-8000-0000000000${(team.operators.length + 0xa1).toString(16)}`;
      team.operators.unshift({
        id, fullName: String(body['fullName']).trim(), email, roleId: role.id, state: 'invited', lastSignInAtUtc: null,
        twoStep: true, mobile: (body['mobile'] as string | null) ?? null, department: (body['department'] as string | null) ?? null,
        jobTitle: (body['jobTitle'] as string | null) ?? null, language: body['language'] === 'en' ? 'en' : 'ar',
        createdAtUtc: new Date().toISOString(), invitedBy: { id: platform.operator.id, name: platform.operator.fullName },
        locked: false, version: 'v1',
      });
      return answer(ok({ id }, 201));
    }

    const person = team.operators.find((known) => known.id === parts[1]);
    if (parts.length === 2 && method === 'GET') {
      const refused = need('operators.view');
      if (refused) {
        return answer(refused);
      }
      if (!person) {
        return answer(problem(404, 'NOT_FOUND'));
      }
      const manages = holds('operators.manage');
      const own = person.id === platform.operator.id;
      return answer(
        ok({
          ...row(person),
          mobile: person.mobile, department: person.department, jobTitle: person.jobTitle, language: person.language,
          createdAtUtc: person.createdAtUtc, invitedBy: person.invitedBy, firstOperator: person.invitedBy === null,
          locked: person.locked, permissions: roleOf(person.roleId).codes,
          performance: [
            { key: 'activeSessions', value: person.state === 'active' ? 1 : 0 },
            // Spec 015, FR-030: the clinics whose account manager this operator is.
            { key: 'clinicsManaged', value: 3 },
          ],
          activity:
            person.lastSignInAtUtc === null
              ? []
              : [
                  { kind: 'signed-in', name: null, role: null, roleKey: null, device: 'Chrome على Windows', atUtc: person.lastSignInAtUtc },
                  { kind: 'operator.invited', name: 'نور خالد', role: null, roleKey: 'viewer', device: null, atUtc: ago(60 * 27) },
                  { kind: 'operator.role-changed', name: 'كريم عادل', role: 'وكيل دعم فني', roleKey: null, device: null, atUtc: ago(60 * 50) },
                  { kind: 'opsrole.created', name: 'نجاح العملاء', role: null, roleKey: null, device: null, atUtc: ago(60 * 24 * 21) },
                ],
          version: person.version,
          can: {
            edit: manages,
            suspend: manages && person.state === 'active' && !own,
            reactivate: manages && person.state === 'suspended',
            sendLink: manages && person.state !== 'suspended',
            unlock: manages && person.state === 'active' && person.locked,
          },
        }),
      );
    }

    const refused = need('operators.manage');
    if (refused) {
      return answer(refused);
    }
    if (!person) {
      return answer(problem(404, 'NOT_FOUND'));
    }

    if (method === 'PUT') {
      if (body['version'] !== person.version) {
        return answer(problem(409, 'OPS_CHANGED_ELSEWHERE'));
      }
      if (body['roleId'] !== person.roleId) {
        const role = team.roles.find((known) => known.id === body['roleId']);
        if (person.id === platform.operator.id) {
          return answer(problem(403, 'OPS_OWN_ROLE'));
        }
        if (!role) {
          return answer(problem(400, 'VALIDATION_FAILED', { errors: { roleId: ['INVALID'] } }));
        }
        if (mayGrant(role) !== null) {
          return answer(problem(403, 'OPS_ROLE_NOT_GRANTABLE'));
        }
        if (person.roleId === SUPER && person.state === 'active' && activeSupers(person.id) === 0) {
          return answer(problem(409, 'OPS_LAST_SUPER_ADMIN'));
        }
        person.roleId = role.id;
      }
      Object.assign(person, {
        fullName: String(body['fullName']).trim(), mobile: body['mobile'] ?? null, department: body['department'] ?? null,
        jobTitle: body['jobTitle'] ?? null, language: body['language'], version: bump(person.version),
      });
      return answer(ok());
    }

    switch (parts[2]) {
      case 'suspend':
        if (person.id === platform.operator.id) {
          return answer(problem(403, 'OPS_OWN_ACCOUNT'));
        }
        if (person.state !== 'active') {
          return answer(problem(409, 'OPS_STATE_CHANGED'));
        }
        if (person.roleId === SUPER && activeSupers(person.id) === 0) {
          return answer(problem(409, 'OPS_LAST_SUPER_ADMIN'));
        }
        person.state = 'suspended';
        return answer(ok());
      case 'reactivate':
        if (person.state !== 'suspended') {
          return answer(problem(409, 'OPS_STATE_CHANGED'));
        }
        person.state = 'active';
        person.locked = false;
        return answer(ok());
      case 'unlock':
        if (!person.locked) {
          return answer(problem(409, 'OPS_STATE_CHANGED'));
        }
        person.locked = false;
        return answer(ok());
      case 'password-link':
        if (team.link === 'too-soon') {
          return answer(problem(409, 'OPS_LINK_TOO_SOON'));
        }
        return answer(team.link === 'not-sent' ? problem(503, 'OPS_LINK_NOT_SENT') : ok(undefined, 202));
      default:
        return answer(problem(404, 'NOT_FOUND'));
    }
  };

  await page.route('**/v1/ops/operators**', handle);
  await page.route('**/v1/ops/roles**', handle);
  await page.route('**/v1/ops/permissions', handle);
  return team;
}
