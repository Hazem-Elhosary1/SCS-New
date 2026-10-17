import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { LanguageService, ScsProblemError } from 'core';
import ar from '../../../public/i18n/ar.json';
import en from '../../../public/i18n/en.json';
import { OpsAccess } from '../access/ops-access.service';
import { PlanFormPage } from './plan-form.page';
import { GridFeature, PlanBody, PlanDetail, PlanGrid, PlansService } from './plans.service';

const settle = () => new Promise((resolve) => setTimeout(resolve));

const feature = (key: string, extra: Partial<GridFeature> = {}): GridFeature => ({
  key,
  nameAr: `ميزة ${key}`,
  nameEn: `Feature ${key}`,
  group: 'channels',
  kind: 'switch',
  unitAr: null,
  unitEn: null,
  status: 'built',
  retired: false,
  sold: true,
  ...extra,
});

const GRID: PlanGrid = {
  items: [
    feature('max_users', { group: 'core', kind: 'limit', unitAr: 'مستخدم', unitEn: 'users' }),
    feature('allow_whatsapp'),
    feature('allow_custom_branding', { group: 'reports', status: 'upcoming' }),
  ],
  badgeHolder: { id: 'other', nameAr: 'الباقة المؤسسية', nameEn: 'Enterprise Plan', badge: 'best' },
};

function detail(extra: Partial<PlanDetail> = {}): PlanDetail {
  return {
    id: 'p1',
    code: 'pro',
    nameAr: 'الباقة الاحترافية',
    nameEn: 'Professional Plan',
    monthlyEgp: 499,
    annualEgp: 4990,
    monthlyUsd: 25,
    annualUsd: 250,
    badge: null,
    state: 'active',
    users: { limit: 10 },
    createdAt: '2026-08-24T12:15:00Z',
    updatedAt: '2026-08-24T12:15:00Z',
    version: 3,
    description: 'باقة متكاملة.',
    disabledReason: null,
    disabledAt: null,
    features: [{ key: 'max_users', limit: 10 }],
    excluded: 2,
    subscriptions: { clinics: 0, monthlyRevenue: 0, newThisMonth: 0, onTrial: 0 },
    clinics: [],
    ...extra,
  };
}

class FakePlans {
  readonly sent: PlanBody[] = [];
  grid: (id: string | null) => Promise<PlanGrid> = async () => GRID;
  read: (id: string) => Promise<PlanDetail> = async () => detail();
  add: (body: PlanBody) => Promise<{ id: string; code: string }> = async (body) => {
    this.sent.push(body);
    return { id: 'new', code: 'x' };
  };
  edit: (id: string, body: PlanBody) => Promise<void> = async (_id, body) => {
    this.sent.push(body);
  };
}

function configure(fake: FakePlans, id: string | null, language: 'ar' | 'en' = 'ar') {
  const spoken = signal(language);
  TestBed.configureTestingModule({
    imports: [
      TranslocoTestingModule.forRoot({
        langs: { ar, en },
        translocoConfig: { availableLangs: ['ar', 'en'], defaultLang: language },
        preloadLangs: true,
      }),
    ],
    providers: [
      provideRouter([{ path: '**', children: [] }]),
      { provide: LanguageService, useValue: { language: spoken, use: (l: 'ar' | 'en') => spoken.set(l) } },
      { provide: OpsAccess, useValue: { can: () => true } },
      { provide: PlansService, useValue: fake },
      {
        provide: ActivatedRoute,
        useValue: { snapshot: { paramMap: convertToParamMap(id === null ? {} : { id }) } },
      },
    ],
  });
}

async function draw(fake: FakePlans, id: string | null = null): Promise<ComponentFixture<PlanFormPage>> {
  configure(fake, id);
  const fixture = TestBed.createComponent(PlanFormPage);
  fixture.detectChanges();
  await settle();
  fixture.detectChanges();
  return fixture;
}

interface Page {
  nameAr: { set: (v: string) => void };
  nameEn: { set: (v: string) => void };
  description: { set: (v: string) => void };
  monthlyEgp: { set: (v: string) => void };
  annualEgp: { set: (v: string) => void };
  monthlyUsd: { set: (v: string) => void };
  annualUsd: { set: (v: string) => void };
  badge: { set: (v: string) => void };
  setOn: (key: string, on: boolean) => void;
  setLimit: (key: string, limit: string) => void;
  save: () => Promise<void>;
}

function fill(page: Page) {
  page.nameAr.set('الباقة الأساسية');
  page.nameEn.set('Basic Plan');
  page.description.set('للعيادات الصغيرة.');
  page.monthlyEgp.set('249');
  page.annualEgp.set('0');
  page.monthlyUsd.set('12');
  page.annualUsd.set('0');
}

const text = (fixture: ComponentFixture<unknown>) => (fixture.nativeElement as HTMLElement).textContent ?? '';

describe('the plan form (spec 013, User Stories 2 and 3)', () => {
  it('draws the fields in the clarified order, the tax line, and the grid by group', async () => {
    const fixture = await draw(new FakePlans());
    const ids = [...(fixture.nativeElement as HTMLElement).querySelectorAll('[data-testid^="field-"]')]
      .map((node) => node.getAttribute('data-testid') ?? '')
      .filter((id) => !/-(hint|error|reason|count)$/u.test(id));
    expect(ids.slice(0, 9)).toEqual([
      'field-name-ar',
      'field-name-en',
      'field-currency',
      'field-monthly-egp',
      'field-annual-egp',
      'field-monthly-usd',
      'field-annual-usd',
      'field-description',
      'field-badge',
    ]);
    expect(ids.at(-1)).toBe('field-note');
    expect(text(fixture)).toContain('الأسعار قبل الضريبة.');
    const groups = [...(fixture.nativeElement as HTMLElement).querySelectorAll('[data-testid="grid-group"]')].map(
      (node) => node.textContent?.trim(),
    );
    expect(groups.length).toBe(3);
    expect((fixture.nativeElement as HTMLElement).querySelectorAll('[data-testid="grid-upcoming"]').length).toBe(1);
  });

  it('keeps a limit’s field disabled until its switch is on, and sends only what is on', async () => {
    const fake = new FakePlans();
    const fixture = await draw(fake);
    const limit = (fixture.nativeElement as HTMLElement).querySelector<HTMLInputElement>('[data-testid="grid-limit"]')!;
    expect(limit.disabled).toBe(true);

    const page = fixture.componentInstance as unknown as Page;
    fill(page);
    page.setOn('max_users', true);
    page.setOn('allow_whatsapp', true);
    fixture.detectChanges();
    expect(limit.disabled).toBe(false);
    await page.save();
    expect(fake.sent[0]!.features).toEqual([
      { key: 'max_users', limit: null },
      { key: 'allow_whatsapp', limit: null },
    ]);
    expect(fake.sent[0]!.monthlyEgp).toBe(249);
  });

  it('says what is wrong at each field before sending anything', async () => {
    const fake = new FakePlans();
    const fixture = await draw(fake);
    const page = fixture.componentInstance as unknown as Page;
    page.monthlyEgp.set('499.999');
    page.setOn('max_users', true);
    page.setLimit('max_users', '0');
    await page.save();
    fixture.detectChanges();
    expect(fake.sent.length).toBe(0);
    expect(text(fixture)).toContain('هذا الحقل مطلوب.');
    expect(text(fixture)).toContain('اكتب سعرًا صحيحًا مثل 499 أو 499.99.');
    expect(text(fixture)).toContain('أدخل حدًّا من 1 إلى 1,000,000، أو اتركه فارغًا لبلا حدّ.');
    // The limit's error is read with its field (T051).
    const field = (fixture.nativeElement as HTMLElement).querySelector('[data-testid="grid-limit"]')!;
    const said = (fixture.nativeElement as HTMLElement).querySelector(`#${field.getAttribute('aria-describedby')}`);
    expect(said?.textContent?.trim()).toBe('أدخل حدًّا من 1 إلى 1,000,000، أو اتركه فارغًا لبلا حدّ.');
  });

  it('names the badge’s holder when this save would take it', async () => {
    const fixture = await draw(new FakePlans());
    const page = fixture.componentInstance as unknown as Page;
    page.badge.set('best_selling');
    fixture.detectChanges();
    expect(text(fixture)).toContain('تحملها الآن «الباقة المؤسسية»؛ حفظ هذه الصفحة ينقلها إلى هنا.');
  });

  it('says a server’s refusal at the top and keeps what was typed', async () => {
    const fake = new FakePlans();
    fake.add = async () => {
      throw new ScsProblemError(
        { type: 'x', title: 'PLAN_FEATURE_NOT_SOLD', status: 409, code: 'PLAN_FEATURE_NOT_SOLD', traceId: 'a'.repeat(32) },
        false,
      );
    };
    const fixture = await draw(fake);
    const page = fixture.componentInstance as unknown as Page;
    fill(page);
    await page.save();
    fixture.detectChanges();
    expect(text(fixture)).toContain('تعذر إضافة الباقة.');
    expect((fixture.componentInstance as unknown as { nameEn: () => string }).nameEn()).toBe('Basic Plan');
  });

  it('edits with the plan filled, its version, and the stale refusal’s reload', async () => {
    const fake = new FakePlans();
    fake.edit = async () => {
      throw new ScsProblemError(
        { type: 'x', title: 'PLAN_CHANGED_ELSEWHERE', status: 409, code: 'PLAN_CHANGED_ELSEWHERE', traceId: 'a'.repeat(32) },
        false,
      );
    };
    const fixture = await draw(fake, 'p1');
    expect(text(fixture)).toContain('تعديل الباقة الاحترافية');
    const page = fixture.componentInstance as unknown as Page;
    await page.save();
    fixture.detectChanges();
    expect(text(fixture)).toContain('تعذر حفظ الباقة.');
    expect(text(fixture)).toContain('تغيّرت هذه البيانات منذ فتحتها.');
    expect((fixture.nativeElement as HTMLElement).querySelector('[data-testid="form-reload"]')).not.toBeNull();
  });

  it('removes the badge for a disabled plan and says why', async () => {
    const fake = new FakePlans();
    fake.read = async () => detail({ state: 'disabled' });
    const fixture = await draw(fake, 'p1');
    expect((fixture.nativeElement as HTMLElement).querySelector('[data-testid="field-badge"]')).toBeNull();
    expect(text(fixture)).toContain('الباقة المعطلة لا تحمل شارة. فعّلها أولًا لتختار لها شارة.');
  });

  it('marks a kept retired feature in an edited plan', async () => {
    const fake = new FakePlans();
    fake.grid = async () => ({ ...GRID, items: [...GRID.items, feature('old', { retired: true })] });
    fake.read = async () => detail({ features: [{ key: 'old', limit: null }] });
    const fixture = await draw(fake, 'p1');
    expect((fixture.nativeElement as HTMLElement).querySelector('[data-testid="grid-retired"]')?.textContent).toContain(
      'موقوفة',
    );
  });
});
