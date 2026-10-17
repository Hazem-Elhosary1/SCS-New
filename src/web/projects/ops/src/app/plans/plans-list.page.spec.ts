import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { LanguageService } from 'core';
import ar from '../../../public/i18n/ar.json';
import en from '../../../public/i18n/en.json';
import { OpsAccess } from '../access/ops-access.service';
import { PlansListPage } from './plans-list.page';
import { PlanRow, PlansService, PlansView } from './plans.service';

const settle = () => new Promise((resolve) => setTimeout(resolve));

function plan(id: string, extra: Partial<PlanRow> = {}): PlanRow {
  return {
    id,
    code: id,
    nameAr: `باقة ${id}`,
    nameEn: `Plan ${id}`,
    monthlyEgp: 499,
    annualEgp: 4990,
    monthlyUsd: 25,
    annualUsd: 250,
    badge: null,
    state: 'active',
    users: { limit: 10 },
    clinics: 0,
    createdAt: '2026-08-24T12:15:00Z',
    updatedAt: '2026-08-24T12:15:00Z',
    version: 1,
    ...extra,
  };
}

const view = (items: PlanRow[], manage = true): PlansView => ({
  items,
  cards: {
    total: items.length,
    disabled: items.filter((p) => p.state === 'disabled').length,
    clinics: 0,
    clinicsNewThisMonth: 0,
    mostWanted: null,
    revenue: null,
  },
  can: { manage },
});

class FakePlans {
  list: () => Promise<PlansView> = async () => view([]);
}

function configure(language: 'ar' | 'en', codes: string[], fake: FakePlans) {
  const spoken = signal(language);
  const held = signal(new Set(codes));
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
      { provide: OpsAccess, useValue: { can: (code: string) => held().has(code) } },
      { provide: PlansService, useValue: fake },
    ],
  });
}

const all = (fixture: ComponentFixture<unknown>, testId: string) =>
  [...(fixture.nativeElement as HTMLElement).querySelectorAll(`[data-testid="${testId}"]`)].map(
    (node) => node.textContent?.trim() ?? '',
  );

const has = (fixture: ComponentFixture<unknown>, testId: string) =>
  (fixture.nativeElement as HTMLElement).querySelector(`[data-testid="${testId}"]`) !== null;

async function draw(fake: FakePlans, codes = ['billing.view', 'billing.managePlans'], language: 'ar' | 'en' = 'ar') {
  configure(language, codes, fake);
  const fixture = TestBed.createComponent(PlansListPage);
  fixture.detectChanges();
  await settle();
  fixture.detectChanges();
  return fixture;
}

describe('the plans list (spec 013, User Story 1)', () => {
  it('says there are no plans, with the BRD’s words, and offers to add one to a manager', async () => {
    const fixture = await draw(new FakePlans());
    const empty = all(fixture, 'plans-empty')[0];
    expect(empty).toContain('لا توجد باقات');
    expect(empty).toContain('أضف أول باقة لتتمكن العيادات من الاشتراك فيها.');
    expect(has(fixture, 'plans-empty-add')).toBe(true);
    expect(all(fixture, 'card-total')[0]).toContain('لا توجد بيانات');
  });

  it('orders active plans by price, then disabled ones, equal prices by name', async () => {
    const fake = new FakePlans();
    fake.list = async () =>
      view([
        plan('c', { nameAr: 'ب', monthlyEgp: 499 }),
        plan('d', { monthlyEgp: 9, state: 'disabled' }),
        plan('a', { nameAr: 'ج', monthlyEgp: 249 }),
        plan('b', { nameAr: 'أ', monthlyEgp: 499 }),
      ]);
    const fixture = await draw(fake);
    const ids = [...(fixture.nativeElement as HTMLElement).querySelectorAll('[data-testid="plan-row"]')].map(
      (row) => row.getAttribute('data-id'),
    );
    expect(ids).toEqual(['a', 'b', 'c', 'd']);
  });

  it('writes each row as the frame and the BRD do', async () => {
    const fake = new FakePlans();
    fake.list = async () =>
      view([plan('p', { monthlyEgp: 249, clinics: 1, users: { limit: 3 } }), plan('q', { users: { limit: null }, clinics: 18 })]);
    const fixture = await draw(fake);
    expect(all(fixture, 'plan-monthly')).toContain('249 ج.م / شهريًا');
    expect(all(fixture, 'plan-clinics')).toEqual(['عيادة واحدة', '18 عيادة']);
    expect(all(fixture, 'plan-max')).toEqual(['حتى 3 مستخدم', 'مستخدمون غير محدودين']);
    expect(all(fixture, 'plan-state')).toEqual(['نشطة', 'نشطة']);
  });

  it('offers only the states that have plans', async () => {
    const fake = new FakePlans();
    fake.list = async () => view([plan('a'), plan('b')]);
    const fixture = await draw(fake);
    const page = fixture.componentInstance as unknown as { stateOptions: () => { value: string }[] };
    expect(page.stateOptions().map((o) => o.value)).toEqual(['active']);
  });

  it('shows a viewer no add button and no row menu', async () => {
    const fake = new FakePlans();
    fake.list = async () => view([plan('a')], false);
    const fixture = await draw(fake, ['billing.view']);
    expect(has(fixture, 'add-plan')).toBe(false);
    expect(has(fixture, 'row-menu')).toBe(false);
    expect(has(fixture, 'open-catalogue')).toBe(true);
  });

  it('says the list could not be read, with the console’s retry word', async () => {
    const fake = new FakePlans();
    fake.list = async () => {
      throw new Error('offline');
    };
    const fixture = await draw(fake);
    const failed = all(fixture, 'plans-failed')[0];
    expect(failed).toContain('حدث خطأ ما');
    expect(failed).toContain('تعذر تحميل قائمة الباقات. تحقق من اتصالك وحاول مرة أخرى.');
    expect(all(fixture, 'plans-retry')[0]).toBe('حاول مرة أخرى');
  });

  it('pages by eight', async () => {
    const fake = new FakePlans();
    fake.list = async () => view(Array.from({ length: 10 }, (_, i) => plan(`p${i}`, { monthlyEgp: i })));
    const fixture = await draw(fake, undefined, 'en');
    expect(all(fixture, 'plan-row').length).toBe(8);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Showing 1–8 of 10 plans');
  });
});
