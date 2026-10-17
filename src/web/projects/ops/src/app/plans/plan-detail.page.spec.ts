import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { LanguageService } from 'core';
import ar from '../../../public/i18n/ar.json';
import en from '../../../public/i18n/en.json';
import { OpsAccess } from '../access/ops-access.service';
import { DisablePlanDialog } from './disable-plan.dialog';
import { FeatureRow, FeaturesService, FeaturesView } from './features.service';
import { PlanDetail, PlansService } from './plans.service';
import { PlanDetailPage } from './plan-detail.page';

const settle = () => new Promise((resolve) => setTimeout(resolve));

const feature = (key: string, extra: Partial<FeatureRow> = {}): FeatureRow => ({
  key,
  nameAr: `ميزة ${key}`,
  nameEn: `Feature ${key}`,
  descriptionAr: null,
  descriptionEn: null,
  group: 'channels',
  kind: 'switch',
  unitAr: null,
  unitEn: null,
  baseline: null,
  specialty: null,
  status: 'built',
  isSold: true,
  isRetired: false,
  sortOrder: 10,
  monthly: false,
  plans: 1,
  version: 1,
  ...extra,
});

const CATALOGUE: FeaturesView = {
  items: [
    feature('max_users', { group: 'core', kind: 'limit', unitAr: 'مستخدم', unitEn: 'users' }),
    feature('allow_whatsapp'),
    feature('message_credits', { kind: 'limit', unitAr: 'رسالة/شهر', unitEn: 'msgs / mo', sortOrder: 20 }),
  ],
  cards: { total: 3, groups: 2, built: 3, upcoming: 0, soldInPlans: 1, plans: 1 },
  can: { manage: true },
};

function plan(extra: Partial<PlanDetail> = {}): PlanDetail {
  return {
    id: 'p1',
    code: 'professional-plan',
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
    version: 1,
    description: 'باقة متكاملة.',
    disabledReason: null,
    disabledAt: null,
    features: [
      { key: 'max_users', limit: 10 },
      { key: 'allow_whatsapp', limit: null },
      { key: 'message_credits', limit: 1000 },
    ],
    excluded: 19,
    subscriptions: { clinics: 0, monthlyRevenue: 0, newThisMonth: 0, onTrial: 0 },
    clinics: [],
    ...extra,
  };
}

function configure(codes: string[], read: () => Promise<PlanDetail>) {
  const spoken = signal<'ar' | 'en'>('ar');
  TestBed.configureTestingModule({
    imports: [
      TranslocoTestingModule.forRoot({
        langs: { ar, en },
        translocoConfig: { availableLangs: ['ar', 'en'], defaultLang: 'ar' },
        preloadLangs: true,
      }),
    ],
    providers: [
      provideRouter([{ path: '**', children: [] }]),
      { provide: LanguageService, useValue: { language: spoken, use: (l: 'ar' | 'en') => spoken.set(l) } },
      { provide: OpsAccess, useValue: { can: (code: string) => codes.includes(code) } },
      { provide: PlansService, useValue: { read } },
      { provide: FeaturesService, useValue: { list: async () => CATALOGUE } },
      { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: 'p1' }) } } },
    ],
  });
}

async function draw(codes: string[], read: () => Promise<PlanDetail>): Promise<ComponentFixture<PlanDetailPage>> {
  configure(codes, read);
  const fixture = TestBed.createComponent(PlanDetailPage);
  fixture.detectChanges();
  await settle();
  fixture.detectChanges();
  return fixture;
}

const text = (fixture: ComponentFixture<unknown>, testId: string) =>
  (fixture.nativeElement as HTMLElement).querySelector(`[data-testid="${testId}"]`)?.textContent?.trim() ?? '';

describe('a plan’s detail (spec 013, User Story 4)', () => {
  it('reads the plan’s information as §5-46 gives it', async () => {
    const fixture = await draw(['billing.view', 'billing.managePlans'], async () => plan());
    expect(text(fixture, 'info-monthly')).toBe('499 ج.م · 25 دولار');
    expect(text(fixture, 'info-annual')).toBe('4,990 ج.م · 250 دولار');
    expect(text(fixture, 'info-users')).toBe('حتى 10 مستخدم');
    expect(text(fixture, 'info-code')).toBe('professional-plan');
    expect(text(fixture, 'detail-under')).toContain('professional-plan · أضيفت');
  });

  it('lists what it includes by group with each value, and closes with what it leaves', async () => {
    const fixture = await draw(['billing.view', 'billing.managePlans'], async () => plan());
    const values = [...(fixture.nativeElement as HTMLElement).querySelectorAll('[data-testid="included-value"]')].map(
      (node) => node.textContent?.trim(),
    );
    expect(values).toEqual(['حتى 10 مستخدم', '✓', '1,000 رسالة/شهر']);
    expect(text(fixture, 'features-closing')).toContain('لا تشمل الباقة 19 ميزة أخرى من الكتالوج.');
    expect(text(fixture, 'features-closing')).toContain('تُعدَّل الميزات وحدودها من «تعديل».');
  });

  it('says there are no clinics yet, no annual price, and a disabled plan’s reason', async () => {
    const fixture = await draw(['billing.view'], async () =>
      plan({ annualEgp: 0, annualUsd: 0, state: 'disabled', disabledReason: 'قديمة', features: [] }),
    );
    expect(text(fixture, 'clinics-none')).toBe('لا توجد عيادات مشتركة في هذه الباقة حتى الآن.');
    expect(text(fixture, 'info-annual')).toBe('بلا سعر سنوي — السنة 12 × الشهري');
    expect(text(fixture, 'info-reason')).toBe('قديمة');
    expect(text(fixture, 'features-none')).toBe('لا توجد ميزات مفعّلة');
  });

  it('lets the EGP annual price decide «بلا سعر سنوي», whatever the reserved USD one is', async () => {
    const fixture = await draw(['billing.view'], async () => plan({ annualEgp: 0, annualUsd: 250 }));
    expect(text(fixture, 'info-annual')).toBe('بلا سعر سنوي — السنة 12 × الشهري');
  });

  it('shows a viewer no actions and the closing line without «تعديل»', async () => {
    const fixture = await draw(['billing.view'], async () => plan());
    expect((fixture.nativeElement as HTMLElement).querySelector('[data-testid="detail-actions"]')).toBeNull();
    expect(text(fixture, 'features-closing')).not.toContain('«تعديل»');
  });

  it('says it could not be read, with the console’s retry word', async () => {
    const fixture = await draw(['billing.view'], async () => {
      throw new Error('offline');
    });
    expect(text(fixture, 'detail-failed')).toContain('تعذر تحميل هذه الباقة');
    expect(text(fixture, 'detail-retry')).toBe('حاول مرة أخرى');
  });
});

describe('the disable dialog (spec 013, User Story 5)', () => {
  function open(clinics: number, disable: (id: string, reason: string | null) => Promise<void> = async () => undefined) {
    TestBed.configureTestingModule({
      imports: [
        TranslocoTestingModule.forRoot({
          langs: { ar, en },
          translocoConfig: { availableLangs: ['ar', 'en'], defaultLang: 'ar' },
          preloadLangs: true,
        }),
      ],
      providers: [
        { provide: LanguageService, useValue: { language: signal('ar'), use: () => undefined } },
        { provide: PlansService, useValue: { disable } },
      ],
    });
    HTMLDialogElement.prototype.showModal ??= function (this: HTMLDialogElement) {
      this.setAttribute('open', '');
    };
    HTMLDialogElement.prototype.close ??= function (this: HTMLDialogElement) {
      this.removeAttribute('open');
    };
    const fixture = TestBed.createComponent(DisablePlanDialog);
    fixture.componentRef.setInput('plan', { id: 'p1', nameAr: 'الباقة الاحترافية', nameEn: 'Professional Plan', clinics });
    fixture.detectChanges();
    return fixture;
  }

  it('warns of the plan’s active clinics only when it has any', () => {
    expect(text(open(64), 'disable-warning')).toBe('باقة «الباقة الاحترافية» عليها 64 عيادة نشطة حاليًا');
    TestBed.resetTestingModule();
    expect((open(0).nativeElement as HTMLElement).querySelector('[data-testid="disable-warning"]')).toBeNull();
  });

  it('sends the reason trimmed, or none, and says a failure in the dialog', async () => {
    const sent: (string | null)[] = [];
    const fixture = open(0, async (_id, reason) => {
      sent.push(reason);
      throw new Error('refused');
    });
    const dialog = fixture.componentInstance as unknown as { reason: { set: (v: string) => void }; disable: () => Promise<void> };
    dialog.reason.set('   ');
    await dialog.disable();
    fixture.detectChanges();
    expect(text(fixture, 'disable-failed')).toBe('تعذر تعطيل الباقة.');
    expect(sent).toEqual([null]);
    dialog.reason.set('  استُبدلت  ');
    await dialog.disable();
    expect(sent).toEqual([null, 'استُبدلت']);
  });
});
