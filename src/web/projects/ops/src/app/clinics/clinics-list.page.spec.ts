import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { LanguageService } from 'core';
import ar from '../../../public/i18n/ar.json';
import en from '../../../public/i18n/en.json';
import { OpsAccess, OpsCan } from '../access/ops-access.service';
import { ClinicsListPage } from './clinics-list.page';
import { ClinicRow, ClinicsPage, ClinicsQuery, ClinicsService } from './clinics.service';

const settle = () => new Promise((resolve) => setTimeout(resolve));
const list = ar.app.clinics.list;

function row(id: string, extra: Partial<ClinicRow> = {}): ClinicRow {
  return {
    id,
    slug: id,
    nameAr: 'عيادة النور',
    nameEn: 'Al Noor Clinic',
    createdAt: '2026-10-01T08:40:00Z',
    createdBy: { id: 'o1', name: 'سارة' },
    plan: { id: 'plan-basic', nameAr: 'الباقة الأساسية', nameEn: 'Basic Plan' },
    status: { code: 'trial', ar: 'تجربة', en: 'Trial', tone: 'info' },
    length: { code: 'trial', days: 14 },
    // A minute short of four days: the page reads its clock a moment before the row is made.
    endsAt: new Date(Date.now() + 4 * 86_400_000 - 60_000).toISOString(),
    ownerName: 'أحمد علي',
    ownerEmail: 'owner@example.com',
    ownerMobile: null,
    ...extra,
  };
}

function view(items: ClinicRow[], total = items.length, edit = true): ClinicsPage {
  return {
    items,
    total,
    page: 1,
    size: 8,
    cards: {
      total,
      newThisMonth: total,
      active: 0,
      activeShare: 0,
      trial: total,
      trialEndingSoon: total > 0,
      needsFollowUp: { overdue: 0, readOnly: 0, suspended: 0 },
    },
    facets: { statuses: ['trial', 'active'], plans: [{ id: 'plan-basic', nameAr: 'الباقة الأساسية', nameEn: 'Basic Plan' }], lengths: ['trial'], endsWithin: [7, 14, 30] },
    can: { add: edit, edit },
  };
}

class FakeClinics {
  readonly asked: ClinicsQuery[] = [];
  answer: (query: ClinicsQuery) => Promise<ClinicsPage> = async () => view([]);
  list = (query: ClinicsQuery) => {
    this.asked.push(query);
    return this.answer(query);
  };
}

async function draw(fake: FakeClinics, codes = [OpsCan.clinicsView, OpsCan.clinicsEdit], language: 'ar' | 'en' = 'ar') {
  const spoken = signal<'ar' | 'en'>(language);
  const held = new Set<string>(codes);
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
      { provide: OpsAccess, useValue: { can: (code: string) => held.has(code) } },
      { provide: ClinicsService, useValue: fake },
    ],
  });
  const fixture = TestBed.createComponent(ClinicsListPage);
  fixture.detectChanges();
  await settle();
  fixture.detectChanges();
  return fixture;
}

const element = (fixture: ComponentFixture<unknown>) => fixture.nativeElement as HTMLElement;
const has = (fixture: ComponentFixture<unknown>, testId: string) => element(fixture).querySelector(`[data-testid="${testId}"]`) !== null;
const all = (fixture: ComponentFixture<unknown>, testId: string) =>
  [...element(fixture).querySelectorAll(`[data-testid="${testId}"]`)].map((node) => node.textContent?.trim() ?? '');

describe('the clinics list (spec 015, User Story 2)', () => {
  it('says there are no clinics yet and offers the add to an editor', async () => {
    const fixture = await draw(new FakeClinics());
    expect(has(fixture, 'clinics-empty')).toBe(true);
    expect(element(fixture).textContent).toContain(list.emptyTitle);
    expect(has(fixture, 'clinics-empty-add')).toBe(true);
  });

  it('offers no add and no row menu to a viewer', async () => {
    const fake = new FakeClinics();
    fake.answer = async () => view([row('c1')], 1, false);
    const fixture = await draw(fake, [OpsCan.clinicsView]);
    expect(has(fixture, 'clinics-add')).toBe(false);
    expect(has(fixture, 'row-menu')).toBe(false);
    expect(all(fixture, 'clinic-row').length).toBe(1);
  });

  it('draws a row: the mark, the name, the plan, the status, the trial’s length and its end', async () => {
    const fake = new FakeClinics();
    fake.answer = async () => view([row('c1')]);
    const fixture = await draw(fake);
    expect(all(fixture, 'clinic-mark')).toEqual(['ن']);
    expect(all(fixture, 'clinic-name')[0]).toContain('عيادة النور');
    expect(all(fixture, 'clinic-plan')).toEqual(['الباقة الأساسية']);
    expect(all(fixture, 'clinic-status')).toEqual(['تجربة']);
    expect(all(fixture, 'clinic-length')).toEqual(['تجربة 14 يومًا']);
    expect(all(fixture, 'clinic-ends')[0]).toContain('خلال 4 أيام');
    expect(has(fixture, 'row-menu')).toBe(true);
  });

  it('opens a clinic from its row', async () => {
    const fake = new FakeClinics();
    fake.answer = async () => view([row('c1')]);
    const fixture = await draw(fake);
    element(fixture).querySelector<HTMLElement>('[data-testid="clinic-row"]')!.click();
    await settle();
    expect(TestBed.inject(Router).url).toBe('/clinics/c1');
  });

  it('asks again for the ending order, from the first page, and says nothing matched', async () => {
    const fake = new FakeClinics();
    fake.answer = async (query) => (query.sort === 'ends-asc' ? { ...view([], 0), cards: view([row('c1')]).cards } : view([row('c1')]));
    const fixture = await draw(fake);
    element(fixture).querySelector<HTMLButtonElement>('[data-testid="sort-ends"]')!.click();
    await settle();
    fixture.detectChanges();
    expect(fake.asked.at(-1)).toMatchObject({ sort: 'ends-asc', page: 1, size: 8 });
    expect(has(fixture, 'clinics-no-results')).toBe(true);
  });

  it('joins the chosen statuses with the page language\u2019s comma', async () => {
    const fake = new FakeClinics();
    fake.answer = async () => view([row('c1')]);
    for (const [language, joined] of [['en', 'Status: Trial, Active'], ['ar', 'الحالة: تجربة، نشطة']] as const) {
      TestBed.resetTestingModule();
      const fixture = await draw(fake, undefined, language);
      const page = fixture.componentInstance as unknown as {
        filters: { set: (f: object) => void };
        applied: () => { label: string }[];
      };
      page.filters.set({ statuses: ['trial', 'active'], plan: '', length: '', endsWithin: '' });
      expect(page.applied()[0]!.label).toBe(joined);
    }
  });

  it('offers to try again when the list cannot be read', async () => {
    const fake = new FakeClinics();
    fake.answer = async () => {
      throw new Error('down');
    };
    const fixture = await draw(fake);
    expect(has(fixture, 'clinics-error')).toBe(true);
    fake.answer = async () => view([row('c1')]);
    element(fixture).querySelector<HTMLButtonElement>('[data-testid="clinics-retry"]')!.click();
    await settle();
    fixture.detectChanges();
    expect(all(fixture, 'clinic-row').length).toBe(1);
  });
});
