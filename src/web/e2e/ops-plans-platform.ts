import { Page, Route } from '@playwright/test';
import { OPS, OpsPlatform } from './ops-platform';
import { OpsCatalogue } from './ops-features-platform';

/**
 * Plays the plans' addresses for the operators console (spec 013; contracts/plans-api.md): plans
 * held in memory — none by default, as the product ships — with the refusals the real platform
 * gives, and the clinics on them as a test gives them (spec 015 and 016 count them for real). The
 * rules are the contract's, written again here on purpose: a browser test checks what the console
 * does with an answer; the answers themselves are proved by the platform's own tests. Call after
 * `opsPlatform`, `opsTeam` and `opsFeatures`, whose catalogue the grid is made of; the catalogue's
 * plan counts follow the plans held here.
 */
export interface FakePlan {
  id: string;
  code: string;
  nameAr: string;
  nameEn: string;
  description: string;
  monthlyEgp: number;
  annualEgp: number;
  monthlyUsd: number;
  annualUsd: number;
  badge: 'best' | 'most_popular' | 'best_selling' | null;
  state: 'active' | 'disabled';
  disabledReason: string | null;
  disabledAt: string | null;
  createdAt: string;
  updatedAt: string;
  version: number;
  features: { key: string; limit: number | null }[];
}

export interface FakePlanClinic {
  id: string;
  nameAr: string;
  nameEn: string;
  stateAr: string;
  stateEn: string;
  trialEndsAt?: string | null;
}

/** The clinics on one plan, as a test gives them. */
export interface FakeSubscribers {
  clinics: number;
  newThisMonth: number;
  onTrial: number;
  monthlyRevenue: number;
  list: FakePlanClinic[];
}

export interface OpsPlans {
  plans: FakePlan[];
  /** Clinics by plan id: none until spec 015, unless a test says so. */
  subscribers: Record<string, FakeSubscribers>;
  /** The platform's monthly plan revenue now and at the end of last month; null with no clinics. */
  revenue: { now: number; lastMonthEnd: number | null } | null;
  /** Makes an address fail: `GET /v1/ops/plans` → a status. */
  failing: Record<string, number>;
  /** Milliseconds every plans address waits, to look at the loading states. */
  delay: number;
}

const BADGES = ['best', 'most_popular', 'best_selling'];
const AMOUNT_MAX = 9_999_999_999.99;

let made = 0;
const newId = () => `00000000-0000-4000-8000-${String(++made).padStart(12, '0')}`;

/** The English name lower-cased, each run of other characters one hyphen (research R3). */
export function planCode(nameEn: string, taken: string[]): string {
  const base =
    nameEn
      .toLowerCase()
      .replace(/[^a-z0-9]+/gu, '-')
      .replace(/^-+|-+$/gu, '')
      .slice(0, 100)
      .replace(/-+$/u, '') || 'plan';
  if (!taken.includes(base)) return base;
  for (let n = 2; ; n++) if (!taken.includes(`${base}-${n}`)) return `${base}-${n}`;
}

/** A plan with what a test changes; its code from its English name. */
export function fakePlan(start: Partial<FakePlan> & { nameEn: string }): FakePlan {
  const when = '2026-08-24T12:15:00Z';
  return {
    id: newId(),
    code: planCode(start.nameEn, []),
    nameAr: 'باقة',
    description: 'باقة للعيادات.',
    monthlyEgp: 499,
    annualEgp: 4990,
    monthlyUsd: 25,
    annualUsd: 250,
    badge: null,
    state: 'active',
    disabledReason: null,
    disabledAt: null,
    createdAt: when,
    updatedAt: when,
    version: 1,
    features: [],
    ...start,
  };
}

/** The design's four plans (frames `1180:70` / `1180:291`), for the screens' pictures. */
export function designPlans(): FakePlan[] {
  return [
    fakePlan({
      nameAr: 'الباقة الأساسية',
      nameEn: 'Basic Plan',
      monthlyEgp: 249,
      annualEgp: 2490,
      monthlyUsd: 12,
      annualUsd: 120,
      features: [
        { key: 'max_users', limit: 3 },
        { key: 'max_doctors', limit: 1 },
        { key: 'allow_whatsapp', limit: null },
      ],
    }),
    fakePlan({
      nameAr: 'الباقة الاحترافية',
      nameEn: 'Professional Plan',
      description: 'باقة متكاملة للعيادات متوسطة الحجم مع تقارير متقدمة وحجز آلي.',
      monthlyEgp: 499,
      annualEgp: 4990,
      badge: 'most_popular',
      features: [
        { key: 'max_users', limit: 10 },
        { key: 'max_doctors', limit: 5 },
        { key: 'allow_whatsapp', limit: null },
        { key: 'allow_telegram', limit: null },
        { key: 'bot_assistant', limit: null },
        { key: 'message_credits', limit: 1000 },
        { key: 'allow_analytics', limit: null },
        { key: 'allow_insurance', limit: null },
        { key: 'allow_refunds', limit: null },
        { key: 'ai_xray_analysis', limit: 200 },
        { key: 'ai_voice_notes', limit: 300 },
        { key: 'reminders_confirm', limit: null },
        { key: 'public_booking', limit: null },
        { key: 'teleconsult', limit: null },
        { key: 'teleconsult_minutes', limit: 600 },
      ],
    }),
    fakePlan({
      nameAr: 'الباقة المؤسسية',
      nameEn: 'Enterprise Plan',
      monthlyEgp: 999,
      annualEgp: 9990,
      monthlyUsd: 50,
      annualUsd: 500,
      features: [{ key: 'max_users', limit: null }],
    }),
    fakePlan({
      nameAr: 'باقة تجريبية (قديمة)',
      nameEn: 'Legacy Trial Plan',
      monthlyEgp: 99,
      annualEgp: 0,
      monthlyUsd: 5,
      annualUsd: 0,
      state: 'disabled',
      disabledReason: 'استُبدلت بالباقة الأساسية.',
      disabledAt: '2026-09-01T09:00:00Z',
      features: [{ key: 'max_users', limit: 1 }],
    }),
  ];
}

const headers = { 'access-control-allow-origin': OPS, 'access-control-allow-credentials': 'true' };

const problem = (status: number, code: string, extra: Record<string, unknown> = {}) => ({
  status,
  headers: { ...headers, 'content-type': 'application/problem+json' },
  body: JSON.stringify({
    type: `urn:scs:error:${code.toLowerCase()}`,
    title: code,
    status,
    code,
    traceId: '4bf92f3577b34da6a3ce929d0e0e4736',
    ...extra,
  }),
});

const ok = (json?: unknown, status = json === undefined ? 204 : 200) => ({
  status,
  headers: { ...headers, ...(json === undefined ? {} : { 'content-type': 'application/json' }) },
  ...(json === undefined ? {} : { body: JSON.stringify(json) }),
});

export async function opsPlans(
  page: Page,
  platform: OpsPlatform,
  catalogue: OpsCatalogue,
  start: Partial<OpsPlans> = {},
  options: { keepCataloguePlans?: boolean } = {},
): Promise<OpsPlans> {
  const book: OpsPlans = {
    plans: [],
    subscribers: {},
    revenue: null,
    failing: {},
    delay: 0,
    ...start,
  };

  const holds = (code: string) => (platform.permissions ?? []).includes(code);
  const feature = (key: string) => catalogue.features.find((f) => f.key === key);
  const counts = (id: string) => book.subscribers[id];

  /** The catalogue's «في الباقات» and «أين تُستعمل» follow the plans held here. */
  const syncCatalogue = () => {
    // A test of the catalogue (spec 012) that gives it plans of its own keeps them.
    if (options.keepCataloguePlans) return;
    const uses: OpsCatalogue['plans'] = {};
    for (const plan of book.plans) {
      for (const row of plan.features) {
        (uses[row.key] ??= []).push({
          id: plan.id,
          nameAr: plan.nameAr,
          nameEn: plan.nameEn,
          state: plan.state,
          clinics: counts(plan.id)?.clinics ?? 0,
        });
      }
    }
    catalogue.plans = uses;
  };
  syncCatalogue();

  const users = (plan: FakePlan) => {
    const row = plan.features.find((f) => f.key === 'max_users');
    return { limit: row ? row.limit : (feature('max_users')?.baseline ?? null) };
  };
  const listRow = (plan: FakePlan) => ({
    id: plan.id,
    code: plan.code,
    nameAr: plan.nameAr,
    nameEn: plan.nameEn,
    monthlyEgp: plan.monthlyEgp,
    annualEgp: plan.annualEgp,
    monthlyUsd: plan.monthlyUsd,
    annualUsd: plan.annualUsd,
    badge: plan.badge,
    state: plan.state,
    users: users(plan),
    clinics: counts(plan.id)?.clinics ?? 0,
    createdAt: plan.createdAt,
    updatedAt: plan.updatedAt,
    version: plan.version,
  });
  const sellable = () =>
    catalogue.features.filter((f) => f.isSold && !f.isRetired).map((f) => f.key);
  const ordered = () =>
    [...catalogue.features].sort(
      (a, b) =>
        ['core', 'channels', 'reports', 'money', 'clinical', 'ai', 'growth'].indexOf(a.group) -
          ['core', 'channels', 'reports', 'money', 'clinical', 'ai', 'growth'].indexOf(b.group) ||
        a.sortOrder - b.sortOrder,
    );
  const gridRow = (f: OpsCatalogue['features'][number]) => ({
    key: f.key,
    nameAr: f.nameAr,
    nameEn: f.nameEn,
    group: f.group,
    kind: f.kind,
    unitAr: f.unitAr,
    unitEn: f.unitEn,
    status: f.status,
    retired: f.isRetired,
    sold: f.isSold,
  });
  const holder = () => {
    const plan = book.plans.find((p) => p.badge !== null);
    return plan ? { id: plan.id, nameAr: plan.nameAr, nameEn: plan.nameEn, badge: plan.badge } : null;
  };

  /** The contract's checks: the fields, then the grid against the catalogue (research R4). */
  const refusal = (body: Record<string, unknown>, plan: FakePlan | null) => {
    const errors: Record<string, string[]> = {};
    const text = (name: string, max: number) => {
      const value = String(body[name] ?? '').trim();
      if (value === '') errors[name] = ['REQUIRED'];
      else if (value.length > max) errors[name] = ['TOO_LONG'];
    };
    text('nameAr', 100);
    text('nameEn', 100);
    text('description', 500);
    for (const name of ['monthlyEgp', 'annualEgp', 'monthlyUsd', 'annualUsd']) {
      const value = body[name];
      if (value === null || value === undefined || value === '') errors[name] = ['REQUIRED'];
      else if (
        typeof value !== 'number' ||
        value < 0 ||
        value > AMOUNT_MAX ||
        Math.round(value * 100) !== value * 100
      )
        errors[name] = ['INVALID'];
    }
    const badge = body['badge'];
    if (badge !== null && badge !== undefined && badge !== '' && !BADGES.includes(String(badge)))
      errors['badge'] = ['INVALID'];
    if (String(body['note'] ?? '').trim().length > 500) errors['note'] = ['TOO_LONG'];

    const rows = (body['features'] as { key?: string; limit?: number | null }[] | undefined) ?? [];
    const seen = new Set<string>();
    let refused: string | null = null;
    rows.forEach((row, i) => {
      const key = row.key ?? '';
      if (key === '') {
        errors[`features[${i}].key`] = ['REQUIRED'];
        return;
      }
      if (seen.has(key)) {
        errors[`features[${i}].key`] = ['INVALID'];
        return;
      }
      seen.add(key);
      const limit = row.limit ?? null;
      if (limit !== null && (limit < 1 || limit > 1_000_000))
        errors[`features[${i}].limit`] = ['INVALID'];
      const f = feature(key);
      if (f === undefined) {
        refused ??= 'PLAN_FEATURE_UNKNOWN';
        return;
      }
      if (limit !== null && f.kind === 'switch') refused ??= 'PLAN_SWITCH_HAS_LIMIT';
      const had = plan?.features.some((x) => x.key === key) ?? false;
      if ((!f.isSold || f.isRetired) && !had) refused ??= 'PLAN_FEATURE_NOT_SOLD';
    });
    if (Object.keys(errors).length > 0) return problem(400, 'VALIDATION_FAILED', { errors });
    if (refused === 'PLAN_FEATURE_NOT_SOLD') return problem(409, refused);
    if (refused !== null) return problem(400, refused);
    const disabled = plan?.state === 'disabled';
    if (disabled && badge) return problem(409, 'PLAN_BADGE_ON_DISABLED');
    return null;
  };

  const take = (plan: FakePlan, body: Record<string, unknown>) => {
    const badge = (body['badge'] || null) as FakePlan['badge'];
    if (badge !== null) {
      for (const other of book.plans) {
        if (other !== plan && other.badge !== null) {
          other.badge = null;
          other.version++;
        }
      }
    }
    Object.assign(plan, {
      nameAr: String(body['nameAr']).trim(),
      nameEn: String(body['nameEn']).trim(),
      description: String(body['description']).trim(),
      monthlyEgp: body['monthlyEgp'] as number,
      annualEgp: body['annualEgp'] as number,
      monthlyUsd: body['monthlyUsd'] as number,
      annualUsd: body['annualUsd'] as number,
      badge,
      updatedAt: new Date().toISOString(),
      features: ((body['features'] as { key: string; limit?: number | null }[]) ?? []).map((r) => ({
        key: r.key,
        limit: r.limit ?? null,
      })),
    });
    syncCatalogue();
  };

  const handle = async (route: Route): Promise<void> => {
    const request = route.request();
    const url = new URL(request.url());
    const method = request.method();
    if (method === 'OPTIONS') {
      await route.fulfill({
        status: 204,
        headers: {
          ...headers,
          'access-control-allow-headers':
            'content-type, traceparent, accept-language, x-scs-request, x-scs-background',
          'access-control-allow-methods': 'GET, POST, PUT, DELETE, OPTIONS',
        },
      });
      return;
    }

    const call = `${method} ${url.pathname}`;
    platform.calls.push(call);
    const body = (request.postData() ? JSON.parse(request.postData()!) : {}) as Record<
      string,
      unknown
    >;
    if (method !== 'GET') platform.bodies.push(body);
    if (book.delay > 0) await new Promise((resolve) => setTimeout(resolve, book.delay));
    const answer = (response: ReturnType<typeof ok>) => route.fulfill(response);

    if (!platform.signedIn) return answer(problem(401, 'UNAUTHENTICATED'));
    if (book.failing[call] !== undefined)
      return answer(problem(book.failing[call], 'INTERNAL_ERROR'));

    const need = method === 'GET' ? 'billing.view' : 'billing.managePlans';
    if (!holds(need)) return answer(problem(403, 'PERMISSION_REQUIRED', { permission: need }));

    const parts = url.pathname
      .replace('/v1/ops/plans', '')
      .split('/')
      .filter(Boolean)
      .map(decodeURIComponent);

    if (parts.length === 0 && method === 'GET') {
      const items = [...book.plans]
        .sort(
          (a, b) =>
            (a.state === 'active' ? 0 : 1) - (b.state === 'active' ? 0 : 1) ||
            a.monthlyEgp - b.monthlyEgp,
        )
        .map(listRow);
      const withClinics = items.filter((p) => p.clinics > 0);
      const most = [...withClinics].sort(
        (a, b) =>
          b.clinics - a.clinics || a.monthlyEgp - b.monthlyEgp || b.createdAt.localeCompare(a.createdAt),
      )[0];
      return answer(
        ok({
          items,
          cards: {
            total: items.length,
            disabled: items.filter((p) => p.state === 'disabled').length,
            clinics: items.reduce((sum, p) => sum + p.clinics, 0),
            clinicsNewThisMonth: book.plans.reduce((sum, p) => sum + (counts(p.id)?.newThisMonth ?? 0), 0),
            mostWanted: most
              ? { id: most.id, nameAr: most.nameAr, nameEn: most.nameEn, clinics: most.clinics }
              : null,
            revenue: book.revenue,
          },
          can: { manage: holds('billing.managePlans') },
        }),
      );
    }

    if (parts.length === 0 && method === 'POST') {
      const refused = refusal(body, null);
      if (refused !== null) return answer(refused);
      const plan = fakePlan({
        nameEn: String(body['nameEn']).trim(),
        code: planCode(
          String(body['nameEn']),
          book.plans.map((p) => p.code),
        ),
        createdAt: new Date().toISOString(),
      });
      take(plan, body);
      book.plans.push(plan);
      syncCatalogue();
      return answer(ok({ id: plan.id, code: plan.code }, 201));
    }

    if (parts[0] === 'grid' && parts.length === 1 && method === 'GET') {
      return answer(
        ok({
          items: ordered()
            .filter((f) => sellable().includes(f.key))
            .map(gridRow),
          badgeHolder: holder(),
        }),
      );
    }

    const plan = book.plans.find((p) => p.id === parts[0]);
    if (plan === undefined) return answer(problem(404, 'NOT_FOUND'));

    if (parts.length === 1 && method === 'GET') {
      const offered = new Set([...sellable(), ...plan.features.map((f) => f.key)]);
      const c = counts(plan.id);
      return answer(
        ok({
          ...listRow(plan),
          description: plan.description,
          disabledReason: plan.disabledReason,
          disabledAt: plan.disabledAt,
          features: plan.features,
          excluded: [...offered].filter((key) => !plan.features.some((f) => f.key === key)).length,
          subscriptions: {
            clinics: c?.clinics ?? 0,
            monthlyRevenue: c?.monthlyRevenue ?? 0,
            newThisMonth: c?.newThisMonth ?? 0,
            onTrial: c?.onTrial ?? 0,
          },
          clinics: c?.list ?? [],
        }),
      );
    }

    if (parts[1] === 'grid' && method === 'GET') {
      const kept = new Set(plan.features.map((f) => f.key));
      return answer(
        ok({
          items: ordered()
            .filter((f) => sellable().includes(f.key) || kept.has(f.key))
            .map(gridRow),
          badgeHolder: holder(),
        }),
      );
    }

    if (parts.length === 1 && method === 'PUT') {
      if (body['version'] !== plan.version) return answer(problem(409, 'PLAN_CHANGED_ELSEWHERE'));
      const refused = refusal(body, plan);
      if (refused !== null) return answer(refused);
      take(plan, body);
      plan.version++;
      return answer(ok());
    }

    if (parts[1] === 'disable' && method === 'POST') {
      const reason = String(body['reason'] ?? '').trim();
      if (reason.length > 500)
        return answer(problem(400, 'VALIDATION_FAILED', { errors: { reason: ['TOO_LONG'] } }));
      if (plan.state === 'active') {
        Object.assign(plan, {
          state: 'disabled',
          disabledReason: reason || null,
          disabledAt: new Date().toISOString(),
          badge: null,
          updatedAt: new Date().toISOString(),
        });
        syncCatalogue();
      }
      return answer(ok());
    }

    if (parts[1] === 'enable' && method === 'POST') {
      if (plan.state === 'disabled') {
        Object.assign(plan, {
          state: 'active',
          disabledReason: null,
          disabledAt: null,
          updatedAt: new Date().toISOString(),
        });
        syncCatalogue();
      }
      return answer(ok());
    }
    return answer(problem(404, 'NOT_FOUND'));
  };

  await page.route('**/v1/ops/plans**', handle);
  return book;
}
