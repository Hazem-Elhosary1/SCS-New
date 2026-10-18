import { Page, Route } from '@playwright/test';
import { OPS, OpsPlatform } from './ops-platform';

/**
 * Plays the clinics' addresses for the operators console (spec 015; contracts/ops-clinics-api.md):
 * clinics held in memory, with the refusals the real platform gives. The rules are the contract's,
 * written again here on purpose: a browser test checks what the console does with an answer; the
 * answers themselves are proved by the platform's own tests. Call after `opsPlatform` and `opsTeam`.
 */
export interface FakeClinic {
  id: string;
  slug: string;
  nameAr: string;
  nameEn: string;
  createdAt: string;
  createdBy: { id: string; name: string } | null;
  version: string;
  status: { code: string; ar: string; en: string; tone: 'info' | 'success' | 'neutral' };
  plan: { id: string; nameAr: string; nameEn: string } | null;
  trialDays: number | null;
  endsAt: string | null;
  address: string | null;
  taxNumber: string | null;
  specialty: string | null;
  defaultLanguage: 'ar' | 'en';
  owner: {
    name: string | null;
    email: string;
    mobile: string | null;
    state: 'invited' | 'active' | 'disabled';
    lastSignInAt: string | null;
    invitation: { state: 'live' | 'lapsed' | 'none'; at: string | null } | null;
  } | null;
  accountManager: { id: string; name: string } | null;
  branches: number;
}

export interface FakeFormPlan {
  id: string;
  nameAr: string;
  nameEn: string;
  monthlyEgp: number;
}

export interface OpsClinics {
  clinics: FakeClinic[];
  /** The plans the form offers (active ones, cheapest first). */
  plans: FakeFormPlan[];
  /** Plans disabled since the form was opened: an add on one is refused with `PLAN_DISABLED`. */
  disabledPlans: string[];
  /** Emails that already hold an account somewhere on the platform. */
  takenEmails: string[];
  /** The active operators, for «تعيين». */
  managers: { id: string; name: string }[];
  /** What the next resend answers: nothing (sent) or a refusal's code. */
  resendRefusal: string | null;
  /** Makes an address fail: `GET /v1/ops/clinics` → a status. */
  failing: Record<string, number>;
  /** Milliseconds every clinics address waits, to look at the loading states. */
  delay: number;
}

export const TRIAL = { code: 'trial', ar: 'تجربة', en: 'Trial', tone: 'info' } as const;
export const ACTIVE = { code: 'active', ar: 'نشطة', en: 'Active', tone: 'success' } as const;

const SPECIALTIES = [
  'dental', 'orthopedics', 'neurology', 'pediatrics', 'internal_medicine', 'general_surgery', 'dermatology', 'obgyn',
  'ophthalmology', 'ent', 'urology', 'cardiology', 'psychiatry', 'nutrition', 'physiotherapy',
];

let made = 0;
const newId = () => `0c11a000-0000-4000-8000-${String(++made).padStart(12, '0')}`;
const days = (n: number) => new Date(Date.now() + n * 86_400_000).toISOString();

/** A clinic with what a test changes: in trial, with its invited owner, by default. */
export function fakeClinic(start: Partial<FakeClinic> & { nameEn: string }): FakeClinic {
  return {
    id: newId(),
    slug: start.nameEn.toLowerCase().replace(/[^a-z0-9]+/gu, '-'),
    nameAr: 'عيادة النور',
    createdAt: '2026-08-25T07:30:00Z',
    createdBy: { id: '0199c0de-0000-7000-8000-0000000000a0', name: 'أحمد سامي' },
    version: 'v1',
    status: TRIAL,
    plan: { id: 'plan-pro', nameAr: 'الاحترافية', nameEn: 'Professional' },
    trialDays: 14,
    endsAt: days(4),
    address: '14 شارع التحرير، القاهرة',
    taxNumber: '310-456-789',
    specialty: 'dental',
    defaultLanguage: 'ar',
    owner: {
      name: 'د. هيثم محمود',
      email: 'haitham@alnoorclinic.example',
      mobile: '+201001234567',
      state: 'invited',
      lastSignInAt: null,
      invitation: { state: 'live', at: days(2.8) },
    },
    accountManager: { id: '0199c0de-0000-7000-8000-0000000000a0', name: 'أحمد سامي' },
    branches: 1,
    ...start,
  };
}

/** The design's eight rows (frames `606:776` / `599:680`), for the screens' pictures. */
export function designClinics(): FakeClinic[] {
  const rows: [string, string, string, Partial<FakeClinic>][] = [
    ['عيادة النور', 'Al Noor Clinic', 'أحمد سامي', { status: ACTIVE, trialDays: null, endsAt: null }],
    ['عيادة الشفاء', 'Al Shifa Clinic', 'منى يوسف', { status: ACTIVE, trialDays: null, endsAt: null }],
    ['مركز نور الطب', 'Nour Medical Center', 'أحمد سامي', { endsAt: days(4) }],
    ['عيادة الرحمة', 'Rahma Clinic', 'كريم عادل', { endsAt: days(9) }],
    ['عيادة الأمل', 'Al Amal Clinic', 'منى يوسف', { status: ACTIVE, trialDays: null, endsAt: null }],
    ['عيادة الوفاء', 'Al Wafaa Clinic', 'كريم عادل', { endsAt: days(12) }],
    ['مركز السلام الطبي', 'Al Salam Medical Center', 'أحمد سامي', { status: ACTIVE, trialDays: null, endsAt: null }],
    ['عيادة الياسمين', 'Al Yasmin Clinic', 'منى يوسف', { endsAt: days(9) }],
  ];
  return rows.map(([nameAr, nameEn, by, extra], i) =>
    fakeClinic({
      nameAr,
      nameEn,
      createdAt: new Date(Date.UTC(2026, 7, 25 - i, 7, 30)).toISOString(),
      createdBy: { id: `0199c0de-0000-7000-8000-0000000000a${i}`, name: by },
      ...extra,
    }),
  );
}

const headers = { 'access-control-allow-origin': OPS, 'access-control-allow-credentials': 'true' };

const problem = (status: number, code: string, extra: Record<string, unknown> = {}) => ({
  status,
  headers: { ...headers, 'content-type': 'application/problem+json' },
  body: JSON.stringify({ type: `urn:scs:error:${code.toLowerCase()}`, title: code, status, code, traceId: '4bf92f3577b34da6a3ce929d0e0e4736', ...extra }),
});

const ok = (json?: unknown, status = json === undefined ? 204 : 200) => ({
  status,
  headers: { ...headers, ...(json === undefined ? {} : { 'content-type': 'application/json' }) },
  ...(json === undefined ? {} : { body: JSON.stringify(json) }),
});

export async function opsClinics(page: Page, platform: OpsPlatform, start: Partial<OpsClinics> = {}): Promise<OpsClinics> {
  const book: OpsClinics = {
    clinics: [],
    plans: [
      { id: 'plan-basic', nameAr: 'الأساسية', nameEn: 'Basic', monthlyEgp: 249 },
      { id: 'plan-pro', nameAr: 'الاحترافية', nameEn: 'Professional', monthlyEgp: 499 },
    ],
    disabledPlans: [],
    takenEmails: [],
    managers: [
      { id: '0199c0de-0000-7000-8000-0000000000a0', name: 'أحمد سامي' },
      { id: '0199c0de-0000-7000-8000-0000000000a1', name: 'منى يوسف' },
    ],
    resendRefusal: null,
    failing: {},
    delay: 0,
    ...start,
  };

  const holds = (code: string) => (platform.permissions ?? []).includes(code);

  const row = (c: FakeClinic) => ({
    id: c.id,
    slug: c.slug,
    nameAr: c.nameAr,
    nameEn: c.nameEn,
    createdAt: c.createdAt,
    createdBy: c.createdBy,
    plan: c.plan,
    status: c.status,
    length: c.status.code === 'trial' ? { code: 'trial', days: c.trialDays } : null,
    endsAt: c.status.code === 'trial' ? c.endsAt : null,
    ownerName: c.owner?.name ?? null,
    ownerEmail: c.owner?.email ?? null,
    ownerMobile: c.owner?.mobile ?? null,
  });

  const detail = (c: FakeClinic) => ({
    id: c.id,
    slug: c.slug,
    nameAr: c.nameAr,
    nameEn: c.nameEn,
    createdAt: c.createdAt,
    createdBy: c.createdBy,
    version: c.version,
    status: c.status,
    plan: c.plan,
    billingCycle: null,
    nextRenewal: c.status.code === 'trial' ? c.endsAt : null,
    address: c.address,
    taxNumber: c.taxNumber,
    specialty: c.specialty,
    defaultLanguage: c.defaultLanguage,
    signInAddress: 'https://app.scs-heta.com',
    owner: c.owner,
    accountManager: c.accountManager,
    managers: holds('clinics.edit') ? book.managers : [],
    doctors: 0,
    branches: c.branches,
    can: { edit: holds('clinics.edit') },
  });

  const fields = (body: Record<string, unknown>, adding: boolean) => {
    const errors: Record<string, string[]> = {};
    const text = (name: string, max: number, needed: boolean) => {
      const value = String(body[name] ?? '').trim();
      if (value === '' && needed) errors[name] = ['REQUIRED'];
      else if (value.length > max) errors[name] = ['TOO_LONG'];
    };
    text('nameAr', 200, true);
    text('nameEn', 200, false);
    text('ownerName', 200, true);
    text('address', 300, true);
    const email = String(body['email'] ?? '').trim().toLowerCase();
    if (email === '') errors['email'] = ['REQUIRED'];
    else if (!/^[^@\s]+@[^@\s]+\.[^@\s]+$/u.test(email)) errors['email'] = ['INVALID_FORMAT'];
    else if (book.takenEmails.includes(email)) errors['email'] = ['TAKEN'];
    const mobile = String(body['mobile'] ?? '').replace(/[\s-]/gu, '');
    if (mobile === '') errors['mobile'] = ['REQUIRED'];
    else if (!/^\+?\d{8,15}$/u.test(mobile)) errors['mobile'] = ['MOBILE'];
    const tax = String(body['taxNumber'] ?? '').replace(/[\s-]/gu, '');
    if (tax !== '' && !/^\d{9}$/u.test(tax)) errors['taxNumber'] = ['TAX_NUMBER'];
    if (adding) {
      if (!body['planId']) errors['planId'] = ['REQUIRED'];
      if (!body['specialty']) errors['specialty'] = ['REQUIRED'];
      else if (!SPECIALTIES.includes(String(body['specialty']))) errors['specialty'] = ['INVALID'];
    }
    return errors;
  };

  const handle = async (route: Route) => {
    const request = route.request();
    const url = new URL(request.url());
    const method = request.method();
    if (method === 'OPTIONS') {
      await route.fulfill({
        status: 204,
        headers: {
          ...headers,
          'access-control-allow-headers': 'content-type, traceparent, accept-language, x-scs-request, x-scs-background',
          'access-control-allow-methods': 'GET, POST, PUT, DELETE, OPTIONS',
        },
      });
      return;
    }

    const call = `${method} ${url.pathname}`;
    platform.calls.push(call);
    const body = (request.postData() ? JSON.parse(request.postData()!) : {}) as Record<string, unknown>;
    if (method !== 'GET') platform.bodies.push(body);
    if (book.delay > 0) await new Promise((resolve) => setTimeout(resolve, book.delay));
    const answer = (response: ReturnType<typeof ok>) => route.fulfill(response);

    if (!platform.signedIn) return answer(problem(401, 'UNAUTHENTICATED'));
    const parts = url.pathname.replace('/v1/ops/clinics', '').split('/').filter(Boolean).map(decodeURIComponent);
    const need = method === 'GET' && parts[0] !== 'form' ? 'clinics.view' : 'clinics.edit';
    if (!holds(need)) return answer(problem(403, 'PERMISSION_REQUIRED', { permission: need }));
    if (book.failing[call] !== undefined) return answer(problem(book.failing[call], 'INTERNAL_ERROR'));

    if (parts.length === 0 && method === 'GET') {
      const q = url.searchParams;
      const search = (q.get('search') ?? '').trim().toLowerCase();
      const statuses = q.getAll('status');
      const plan = q.get('plan');
      const length = q.get('length');
      const within = q.get('endsWithin');
      const sort = q.get('sort');
      const size = Math.min(50, Number(q.get('size') ?? 8));
      const all = book.clinics.map(row);
      const searched = all.filter(
        (r) =>
          search === '' ||
          [r.nameAr, r.nameEn, r.ownerName, r.ownerEmail].some((t) => (t ?? '').toLowerCase().includes(search)) ||
          (r.ownerMobile ?? '').includes(search.replace(/[\s-]/gu, '')),
      );
      const filtered = searched
        .filter((r) => statuses.length === 0 || statuses.includes(r.status.code))
        .filter((r) => !plan || r.plan?.id === plan)
        .filter((r) => !length || (r.length?.code ?? 'unset') === length)
        .filter((r) => !within || (r.endsAt !== null && new Date(r.endsAt).getTime() <= Date.now() + Number(within) * 86_400_000));
      const byNew = (a: { createdAt: string }, b: { createdAt: string }) => b.createdAt.localeCompare(a.createdAt);
      const sorted = [...filtered].sort((a, b) => {
        if (sort === 'ends-asc' || sort === 'ends-desc') {
          if (a.endsAt === null || b.endsAt === null) return (a.endsAt === null ? 1 : 0) - (b.endsAt === null ? 1 : 0) || byNew(a, b);
          return (sort === 'ends-asc' ? a.endsAt.localeCompare(b.endsAt) : b.endsAt.localeCompare(a.endsAt)) || byNew(a, b);
        }
        return byNew(a, b);
      });
      const pages = Math.max(1, Math.ceil(sorted.length / size));
      const pageNumber = Math.min(Math.max(1, Number(q.get('page') ?? 1)), pages);
      const active = all.filter((r) => r.status.code === 'active').length;
      const trial = all.filter((r) => r.status.code === 'trial').length;
      const planList = [...new Map(searched.filter((r) => r.plan).map((r) => [r.plan!.id, r.plan!])).values()];
      return answer(
        ok({
          items: sorted.slice((pageNumber - 1) * size, pageNumber * size),
          total: sorted.length,
          page: pageNumber,
          size,
          cards: {
            total: all.length,
            newThisMonth: Math.min(all.length, 2),
            active,
            activeShare: all.length === 0 ? 0 : Math.round((active * 100) / all.length),
            trial,
            trialEndingSoon: all.some((r) => r.endsAt !== null && new Date(r.endsAt).getTime() <= Date.now() + 7 * 86_400_000),
            needsFollowUp: { overdue: 0, readOnly: 0, suspended: 0 },
          },
          facets: {
            statuses: ['active', 'trial'].filter((s) => searched.some((r) => r.status.code === s)),
            plans: planList,
            lengths: ['monthly', 'annual', 'trial', 'unset'].filter((l) => searched.some((r) => (r.length?.code ?? 'unset') === l)),
            endsWithin: [7, 14, 30],
          },
          can: { add: holds('clinics.edit'), edit: holds('clinics.edit') },
        }),
      );
    }

    if (parts[0] === 'form' && method === 'GET') {
      return answer(ok({ plans: book.plans, specialties: SPECIALTIES }));
    }

    if (parts.length === 0 && method === 'POST') {
      const errors = fields(body, true);
      if (Object.keys(errors).length > 0) return answer(problem(400, 'VALIDATION_FAILED', { errors }));
      if (book.disabledPlans.includes(String(body['planId']))) return answer(problem(409, 'PLAN_DISABLED'));
      const plan = book.plans.find((p) => p.id === body['planId']) ?? null;
      const clinic = fakeClinic({
        nameAr: String(body['nameAr']).trim(),
        nameEn: String(body['nameEn'] ?? '').trim() || String(body['nameAr']).trim(),
        createdAt: new Date().toISOString(),
        plan: plan && { id: plan.id, nameAr: plan.nameAr, nameEn: plan.nameEn },
        endsAt: days(14),
        address: String(body['address']).trim(),
        taxNumber: String(body['taxNumber'] ?? '').replace(/[\s-]/gu, '').replace(/^(\d{3})(\d{3})(\d{3})$/u, '$1-$2-$3') || null,
        specialty: String(body['specialty']),
        owner: {
          name: String(body['ownerName']).trim(),
          email: String(body['email']).trim().toLowerCase(),
          mobile: String(body['mobile']).replace(/[\s-]/gu, ''),
          state: 'invited',
          lastSignInAt: null,
          invitation: { state: 'live', at: days(3) },
        },
        accountManager: null,
      });
      book.clinics.unshift(clinic);
      return answer(ok({ id: clinic.id, slug: clinic.slug }, 201));
    }

    const clinic = book.clinics.find((c) => c.id === parts[0]);
    if (clinic === undefined) return answer(problem(404, 'NOT_FOUND'));

    if (parts.length === 1 && method === 'GET') return answer(ok(detail(clinic)));

    if (parts.length === 1 && method === 'PUT') {
      if (body['version'] !== clinic.version) return answer(problem(409, 'CLINIC_CHANGED_ELSEWHERE'));
      const errors = fields(body, false);
      const tax = String(body['taxNumber'] ?? '');
      if (errors['taxNumber'] && (!body['taxNumberSent'] || tax.trim() === (clinic.taxNumber ?? ''))) delete errors['taxNumber'];
      if (String(body['email'] ?? '').trim().toLowerCase() === clinic.owner?.email) delete errors['email'];
      if (String(body['note'] ?? '').trim().length > 500) errors['note'] = ['TOO_LONG'];
      if (Object.keys(errors).length > 0) return answer(problem(400, 'VALIDATION_FAILED', { errors }));
      clinic.nameAr = String(body['nameAr']).trim();
      clinic.nameEn = String(body['nameEn'] ?? '').trim() || clinic.nameAr;
      clinic.address = String(body['address']).trim();
      if (body['taxNumberSent']) clinic.taxNumber = tax.replace(/[\s-]/gu, '').replace(/^(\d{3})(\d{3})(\d{3})$/u, '$1-$2-$3') || null;
      clinic.specialty = (body['specialty'] as string | null) ?? null;
      clinic.defaultLanguage = body['defaultLanguage'] === 'en' ? 'en' : 'ar';
      if (clinic.owner) {
        clinic.owner = {
          ...clinic.owner,
          name: String(body['ownerName']).trim(),
          email: String(body['email']).trim().toLowerCase(),
          mobile: String(body['mobile']).replace(/[\s-]/gu, ''),
        };
      }
      clinic.version = `v${Number(clinic.version.slice(1)) + 1}`;
      return answer(ok());
    }

    if (parts[1] === 'account-manager' && method === 'PUT') {
      if (body['version'] !== clinic.version) return answer(problem(409, 'CLINIC_CHANGED_ELSEWHERE'));
      const chosen = body['operatorId'] as string | null;
      const manager = chosen === null ? null : book.managers.find((m) => m.id === chosen);
      if (manager === undefined) return answer(problem(409, 'OPERATOR_NOT_ACTIVE'));
      clinic.accountManager = manager;
      clinic.version = `v${Number(clinic.version.slice(1)) + 1}`;
      return answer(ok({ accountManager: manager, version: clinic.version }));
    }

    if (parts[1] === 'invitation' && method === 'POST') {
      if (book.resendRefusal !== null) {
        const status = book.resendRefusal === 'RESET_EMAIL_COOLDOWN' ? 429 : book.resendRefusal === 'ACCOUNT_LINK_NOT_SENT' ? 502 : 409;
        return answer(problem(status, book.resendRefusal));
      }
      const at = days(3);
      if (clinic.owner) clinic.owner = { ...clinic.owner, invitation: { state: 'live', at } };
      return answer(ok({ expiresAt: at }));
    }

    if (parts[1] === 'settings' && method === 'GET') {
      return answer(
        ok({ defaultLanguage: clinic.defaultLanguage, timeZone: 'Africa/Cairo', staffAddress: 'https://app.scs-heta.com', can: { edit: holds('clinics.edit') } }),
      );
    }

    return answer(problem(404, 'NOT_FOUND'));
  };

  await page.route('**/v1/ops/clinics**', handle);
  return book;
}
