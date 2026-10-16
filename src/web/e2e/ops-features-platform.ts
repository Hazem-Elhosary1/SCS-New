import { Page, Route } from '@playwright/test';
import { OPS, OpsPlatform } from './ops-platform';

/**
 * Plays the feature catalogue's addresses for the operators console (spec 012;
 * contracts/features-api.md): the 35 seeded features held in memory, with the refusals the real
 * platform gives. The rules are the contract's, written again here on purpose — a browser test
 * checks what the console does with an answer; the answers themselves are proved by the
 * platform's own tests. Call after `opsPlatform` and `opsTeam`, whose role decides what is held.
 */
export interface FakeFeature {
  key: string;
  nameAr: string;
  nameEn: string;
  descriptionAr: string | null;
  descriptionEn: string | null;
  group: string;
  kind: 'switch' | 'limit';
  unitAr: string | null;
  unitEn: string | null;
  baseline: number | null;
  specialty: string | null;
  status: 'built' | 'upcoming';
  isSold: boolean;
  isRetired: boolean;
  sortOrder: number;
  version: number;
}

export interface FakePlanUse {
  id: string;
  name: string;
  state: 'active' | 'inactive';
  clinics: number;
}

export interface OpsCatalogue {
  features: FakeFeature[];
  /** The plans that include a feature, by key: none until spec 013, unless a test says so. */
  plans: Record<string, FakePlanUse[]>;
  /** Makes an address fail: `GET /v1/ops/features` → a status. */
  failing: Record<string, number>;
  /** Milliseconds every catalogue address waits, to look at the loading states. */
  delay: number;
}

const GROUPS = ['core', 'channels', 'reports', 'money', 'clinical', 'ai', 'growth'];
const SPECIALTIES = [
  'dental',
  'orthopedics',
  'neurology',
  'pediatrics',
  'internal_medicine',
  'general_surgery',
  'dermatology',
  'obgyn',
  'ophthalmology',
  'ent',
  'urology',
  'cardiology',
  'psychiatry',
  'nutrition',
  'physiotherapy',
];
const MONTHLY = ['message_credits', 'ai_xray_analysis', 'ai_voice_notes', 'teleconsult_minutes'];

type Seed = [
  key: string,
  group: string,
  nameAr: string,
  nameEn: string,
  unit?: [string, string, number | null],
  specialty?: string,
  sold?: boolean,
];

const SEED: Seed[] = [
  [
    'core_clinic',
    'core',
    'الحجوزات والمرضى والكشف والروشتة',
    'Bookings, patients, exams & prescriptions',
    undefined,
    undefined,
    false,
  ],
  ['max_users', 'core', 'المستخدمون', 'Users', ['مستخدم', 'users', null]],
  ['max_doctors', 'core', 'الأطباء', 'Doctors', ['طبيب', 'doctors', 1]],
  ['max_branches', 'core', 'الفروع', 'Branches', ['فرع', 'branches', 1]],
  ['allow_whatsapp', 'channels', 'قناة واتساب', 'WhatsApp channel'],
  ['allow_telegram', 'channels', 'قناة تيليجرام', 'Telegram channel'],
  ['bot_assistant', 'channels', 'مساعد الحجز', 'Booking assistant'],
  ['message_credits', 'channels', 'رصيد الرسائل', 'Message credits', ['رسالة/شهر', 'msgs / mo', 0]],
  ['allow_analytics', 'reports', 'التقارير المالية والطبية', 'Financial & medical reports'],
  ['allow_custom_branding', 'reports', 'العلامة التجارية المخصصة', 'Custom branding'],
  ['allow_insurance', 'money', 'التأمين', 'Insurance'],
  ['allow_refunds', 'money', 'الاسترداد', 'Refunds'],
  ['itemized_invoice', 'money', 'فاتورة المريض بالبنود', 'Itemised patient invoice'],
  ['doctor_statement', 'money', 'كشف حساب الطبيب', 'Doctor statement'],
  ['deposit_policy', 'money', 'العربون ورسوم عدم الحضور', 'Deposit & no-show fee'],
  [
    'specialty_exam_modules',
    'clinical',
    'وحدات الكشف بحسب التخصص',
    'Specialty exam modules',
    undefined,
    undefined,
    false,
  ],
  ['dental_chart', 'clinical', 'خريطة الأسنان', 'Dental chart', undefined, 'dental'],
  ['treatment_plans', 'clinical', 'خطط العلاج', 'Treatment plans'],
  ['dental_lab', 'clinical', 'معمل الأسنان', 'Dental lab', undefined, 'dental'],
  ['drug_directory', 'clinical', 'دليل الأدوية', 'Drug directory'],
  ['informed_consent', 'clinical', 'الموافقة المستنيرة', 'Informed consent'],
  ['licences_registry', 'clinical', 'ملف الرخص والتغطية', 'Licences & coverage'],
  ['inventory', 'clinical', 'المخزون والمستهلكات', 'Inventory & consumables'],
  [
    'sick_leave_referral',
    'clinical',
    'الإجازة المرضية وخطاب التحويل',
    'Sick leave & referral letter',
  ],
  ['lab_orders', 'clinical', 'طلبات التحاليل والأشعة', 'Lab & imaging orders'],
  [
    'ai_xray_analysis',
    'ai',
    'تحليل الأشعة بالذكاء الاصطناعي',
    'X-ray AI analysis',
    ['تحليل/شهر', 'scans / mo', 0],
  ],
  ['ai_voice_notes', 'ai', 'كتابة الملاحظة بالصوت', 'Voice notes', ['دقيقة/شهر', 'min / mo', 0]],
  ['reminders_confirm', 'growth', 'التذكيرات و«أكّد أو ألغِ»', 'Reminders & confirm-or-release'],
  [
    'queue_screen',
    'growth',
    'شاشة الدور ورابط «دورك»',
    'Queue screen & "your turn" link',
    ['شاشة', 'screens', 0],
  ],
  ['public_booking', 'growth', 'صفحة الحجز العامة', 'Public booking page'],
  ['recall_list', 'growth', 'قائمة المتابعة', 'Recall list'],
  ['weekly_summary', 'growth', 'الملخص الأسبوعي للمالك', 'Owner weekly summary'],
  ['teleconsult', 'growth', 'الكشف عن بُعد', 'Teleconsult'],
  [
    'teleconsult_minutes',
    'growth',
    'دقائق الكشف عن بُعد',
    'Teleconsult minutes',
    ['دقيقة/شهر', 'min / mo', 0],
  ],
  ['patient_feedback', 'growth', 'تقييم المريض بعد الزيارة', 'Patient feedback'],
];

/** The catalogue a new platform starts with: every feature «قادمة». */
export function seededFeatures(): FakeFeature[] {
  const next: Record<string, number> = {};
  return SEED.map(([key, group, nameAr, nameEn, unit, specialty, sold]) => {
    next[group] = (next[group] ?? 0) + 10;
    return {
      key,
      nameAr,
      nameEn,
      descriptionAr: null,
      descriptionEn: null,
      group,
      kind: unit === undefined ? 'switch' : 'limit',
      unitAr: unit?.[0] ?? null,
      unitEn: unit?.[1] ?? null,
      baseline: unit?.[2] ?? null,
      specialty: specialty ?? null,
      status: 'upcoming',
      isSold: sold ?? true,
      isRetired: false,
      sortOrder: next[group],
      version: 1,
    };
  });
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

export async function opsFeatures(
  page: Page,
  platform: OpsPlatform,
  start: Partial<OpsCatalogue> = {},
): Promise<OpsCatalogue> {
  const catalogue: OpsCatalogue = {
    features: seededFeatures(),
    plans: {},
    failing: {},
    delay: 0,
    ...start,
  };

  const holds = (code: string) => (platform.permissions ?? []).includes(code);
  const ordered = () =>
    [...catalogue.features].sort(
      (a, b) => GROUPS.indexOf(a.group) - GROUPS.indexOf(b.group) || a.sortOrder - b.sortOrder,
    );
  const plansOf = (key: string) => catalogue.plans[key] ?? [];
  const row = (f: FakeFeature) => ({
    ...f,
    monthly: MONTHLY.includes(f.key),
    plans: plansOf(f.key).length,
  });
  const nextOrder = (group: string) =>
    Math.max(0, ...catalogue.features.filter((f) => f.group === group).map((f) => f.sortOrder)) +
    10;

  /** The contract's checks: a field out of its rule, then the shape of a switch or a limit. */
  const refusal = (body: Record<string, unknown>, keyed: boolean) => {
    const errors: Record<string, string[]> = {};
    const text = (name: string, max: number, required: boolean) => {
      const value = String(body[name] ?? '').trim();
      if (value === '' && required) errors[name] = ['REQUIRED'];
      else if (value.length > max) errors[name] = ['TOO_LONG'];
    };
    if (keyed) {
      const key = String(body['key'] ?? '');
      if (key === '') errors['key'] = ['REQUIRED'];
      else if (!/^[a-z][a-z0-9_]{1,63}$/u.test(key)) errors['key'] = ['INVALID'];
    }
    text('nameAr', 120, true);
    text('nameEn', 120, true);
    text('unitAr', 40, false);
    text('unitEn', 40, false);
    text('descriptionAr', 500, false);
    text('descriptionEn', 500, false);
    const oneOf = (name: string, list: readonly string[], required: boolean) => {
      const value = body[name];
      if (value === null || value === undefined || value === '') {
        if (required) errors[name] = ['REQUIRED'];
      } else if (!list.includes(String(value))) {
        errors[name] = ['INVALID'];
      }
    };
    oneOf('group', GROUPS, true);
    oneOf('kind', ['switch', 'limit'], true);
    oneOf('status', ['built', 'upcoming'], true);
    oneOf('specialty', SPECIALTIES, false);
    const baseline = body['baseline'];
    if (
      baseline !== null &&
      baseline !== undefined &&
      (typeof baseline !== 'number' || baseline < 0 || baseline > 1_000_000)
    ) {
      errors['baseline'] = ['INVALID'];
    }
    if (Object.keys(errors).length > 0) return problem(400, 'VALIDATION_FAILED', { errors });
    const units = !!String(body['unitAr'] ?? '').trim() && !!String(body['unitEn'] ?? '').trim();
    if (body['kind'] === 'limit' && !units) return problem(400, 'FEATURE_UNITS_REQUIRED');
    if (
      body['kind'] === 'switch' &&
      (body['unitAr'] ||
        body['unitEn'] ||
        (body['baseline'] !== null && body['baseline'] !== undefined))
    ) {
      return problem(400, 'FEATURE_SWITCH_HAS_LIMIT');
    }
    return null;
  };

  const fields = (body: Record<string, unknown>) => ({
    nameAr: String(body['nameAr']).trim(),
    nameEn: String(body['nameEn']).trim(),
    descriptionAr: (body['descriptionAr'] as string | null) ?? null,
    descriptionEn: (body['descriptionEn'] as string | null) ?? null,
    group: String(body['group']),
    kind: body['kind'] as 'switch' | 'limit',
    unitAr: (body['unitAr'] as string | null) ?? null,
    unitEn: (body['unitEn'] as string | null) ?? null,
    baseline: (body['baseline'] as number | null) ?? null,
    specialty: (body['specialty'] as string | null) ?? null,
    status: body['status'] as 'built' | 'upcoming',
    isSold: body['isSold'] !== false,
  });

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
    if (method !== 'GET') {
      platform.bodies.push(body);
    }
    if (catalogue.delay > 0) {
      await new Promise((resolve) => setTimeout(resolve, catalogue.delay));
    }
    const answer = (response: ReturnType<typeof ok>) => route.fulfill(response);

    if (!platform.signedIn) return answer(problem(401, 'UNAUTHENTICATED'));
    if (catalogue.failing[call] !== undefined)
      return answer(problem(catalogue.failing[call], 'INTERNAL_ERROR'));

    const need = method === 'GET' ? 'billing.view' : 'billing.managePlans';
    if (!holds(need)) return answer(problem(403, 'PERMISSION_REQUIRED', { permission: need }));

    const parts = url.pathname
      .replace('/v1/ops/features', '')
      .split('/')
      .filter(Boolean)
      .map(decodeURIComponent);

    if (parts.length === 0 && method === 'GET') {
      const items = ordered().map(row);
      const plans = new Set(
        Object.values(catalogue.plans)
          .flat()
          .map((plan) => plan.id),
      );
      return answer(
        ok({
          items,
          cards: {
            total: items.length,
            groups: new Set(items.map((f) => f.group)).size,
            built: items.filter((f) => f.status === 'built').length,
            upcoming: items.filter((f) => f.status === 'upcoming').length,
            soldInPlans: items.filter((f) => f.plans > 0).length,
            plans: plans.size,
          },
          can: { manage: holds('billing.managePlans') },
        }),
      );
    }

    if (parts.length === 0 && method === 'POST') {
      const refused = refusal(body, true);
      if (refused !== null) return answer(refused);
      if (catalogue.features.some((f) => f.key === body['key']))
        return answer(problem(409, 'FEATURE_KEY_TAKEN'));
      const made = fields(body);
      catalogue.features.push({
        key: String(body['key']),
        ...made,
        isRetired: false,
        sortOrder: nextOrder(made.group),
        version: 1,
      });
      return answer(ok({ key: body['key'] }, 201));
    }

    const feature = catalogue.features.find((f) => f.key === parts[0]);
    if (feature === undefined) return answer(problem(404, 'NOT_FOUND'));

    if (parts.length === 1 && method === 'GET') return answer(ok(row(feature)));
    if (parts[1] === 'plans' && method === 'GET') {
      const used = plansOf(feature.key);
      return answer(ok({ items: used, total: used.reduce((sum, plan) => sum + plan.clinics, 0) }));
    }

    if (parts.length === 1 && method === 'PUT') {
      const refused = refusal(body, false);
      if (refused !== null) return answer(refused);
      if (body['version'] !== feature.version)
        return answer(problem(409, 'FEATURE_CHANGED_ELSEWHERE'));
      const made = fields(body);
      if (feature.isRetired && !made.isSold)
        return answer(problem(409, 'FEATURE_RETIRED_MUST_BE_SOLD'));
      const order = made.group === feature.group ? feature.sortOrder : nextOrder(made.group);
      Object.assign(feature, made, { sortOrder: order, version: feature.version + 1 });
      return answer(ok());
    }

    if (parts[1] === 'retire' && method === 'POST') {
      if (!feature.isSold || feature.isRetired)
        return answer(problem(409, 'FEATURE_STATE_CHANGED'));
      feature.isRetired = true;
      return answer(ok());
    }
    if (parts[1] === 'resume' && method === 'POST') {
      if (!feature.isRetired) return answer(problem(409, 'FEATURE_STATE_CHANGED'));
      feature.isRetired = false;
      return answer(ok());
    }
    return answer(problem(404, 'NOT_FOUND'));
  };

  await page.route('**/v1/ops/features**', handle);
  return catalogue;
}
