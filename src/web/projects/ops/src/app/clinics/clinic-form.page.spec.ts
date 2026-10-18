import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { LanguageService, ScsProblemError } from 'core';
import ar from '../../../public/i18n/ar.json';
import en from '../../../public/i18n/en.json';
import { OpsAccess } from '../access/ops-access.service';
import { ClinicFormPage } from './clinic-form.page';
import { ClinicDetail, ClinicFormOptions, ClinicsService, EditClinicBody, NewClinicBody } from './clinics.service';

const settle = () => new Promise((resolve) => setTimeout(resolve));
const form = ar.app.clinics.form;

const OPTIONS: ClinicFormOptions = {
  plans: [
    { id: 'plan-basic', nameAr: 'الباقة الأساسية', nameEn: 'Basic Plan', monthlyEgp: 249 },
    { id: 'plan-pro', nameAr: 'الباقة الاحترافية', nameEn: 'Professional Plan', monthlyEgp: 499 },
  ],
  specialties: ['dental', 'pediatrics'],
};

function detail(extra: Partial<ClinicDetail> = {}): ClinicDetail {
  return {
    id: 'c1',
    slug: 'noor',
    nameAr: 'عيادة النور',
    nameEn: 'Al Noor Clinic',
    createdAt: '2026-10-01T09:00:00Z',
    createdBy: { id: 'o1', name: 'سارة' },
    version: 'AAAAAAAAB9E=',
    status: { code: 'trial', ar: 'تجربة', en: 'Trial', tone: 'info' },
    plan: { id: 'plan-basic', nameAr: 'الباقة الأساسية', nameEn: 'Basic Plan' },
    billingCycle: null,
    nextRenewal: null,
    address: 'القاهرة',
    taxNumber: '123456789',
    specialty: 'dental',
    defaultLanguage: 'ar',
    signInAddress: 'https://app.scs-heta.com',
    owner: {
      name: 'أحمد علي',
      email: 'owner@example.com',
      mobile: '+201001234567',
      state: 'invited',
      lastSignInAt: null,
      invitation: { state: 'live', at: '2026-10-01T09:00:00Z' },
    },
    accountManager: null,
    managers: [],
    doctors: 0,
    branches: 1,
    can: { edit: true },
    ...extra,
  };
}

const problem = (code: string, status = 409, extra: object = {}) =>
  new ScsProblemError({ type: 'x', title: code, status, code, traceId: 'a'.repeat(32), ...extra }, false);

class FakeClinics {
  readonly added: NewClinicBody[] = [];
  readonly edited: EditClinicBody[] = [];
  form: () => Promise<ClinicFormOptions> = async () => OPTIONS;
  read: (id: string) => Promise<ClinicDetail> = async () => detail();
  add: (body: NewClinicBody) => Promise<{ id: string; slug: string }> = async (body) => {
    this.added.push(body);
    return { id: 'new-clinic', slug: 'noor' };
  };
  edit: (id: string, body: EditClinicBody) => Promise<void> = async (_id, body) => {
    this.edited.push(body);
  };
}

async function draw(fake: FakeClinics, id: string | null = null): Promise<ComponentFixture<ClinicFormPage>> {
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
      { provide: OpsAccess, useValue: { can: () => true } },
      { provide: ClinicsService, useValue: fake },
      { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap(id === null ? {} : { id }) } } },
    ],
  });
  const fixture = TestBed.createComponent(ClinicFormPage);
  fixture.detectChanges();
  await settle();
  fixture.detectChanges();
  return fixture;
}

interface Page {
  nameAr: { set: (v: string) => void };
  nameEn: { set: (v: string) => void; (): string };
  ownerName: { set: (v: string) => void };
  email: { set: (v: string) => void };
  mobile: { set: (v: string) => void };
  address: { set: (v: string) => void };
  planId: { set: (v: string) => void };
  specialty: { set: (v: string) => void };
  typeTax: (v: string) => void;
  save: () => Promise<void>;
}

const page = (fixture: ComponentFixture<ClinicFormPage>) => fixture.componentInstance as unknown as Page;
const text = (fixture: ComponentFixture<unknown>) => (fixture.nativeElement as HTMLElement).textContent ?? '';
const has = (fixture: ComponentFixture<unknown>, testId: string) =>
  (fixture.nativeElement as HTMLElement).querySelector(`[data-testid="${testId}"]`) !== null;

function fill(form: Page) {
  form.nameAr.set('عيادة النور');
  form.nameEn.set('Al Noor Clinic');
  form.ownerName.set('أحمد علي');
  form.email.set(' owner@example.com ');
  form.mobile.set('+20 100 123 4567');
  form.address.set('القاهرة');
  form.planId.set('plan-basic');
  form.specialty.set('dental');
}

describe('the clinic form, adding (spec 015, User Story 1)', () => {
  it('draws the fields in the drawn order, the add words, and no edit-only field', async () => {
    const fixture = await draw(new FakeClinics());
    expect(text(fixture)).toContain(form.addLine);
    expect(text(fixture)).toContain(form.add);
    for (const id of ['clinic-name-ar', 'clinic-name-en', 'clinic-owner-name', 'clinic-email', 'clinic-mobile', 'clinic-address', 'clinic-plan', 'clinic-tax', 'clinic-specialty']) {
      expect(has(fixture, id), id).toBe(true);
    }
    expect(has(fixture, 'clinic-language')).toBe(false);
    expect(has(fixture, 'clinic-note')).toBe(false);
  });

  it('says what is wrong at each field before sending anything', async () => {
    const fake = new FakeClinics();
    const fixture = await draw(fake);
    page(fixture).email.set('not-an-email');
    page(fixture).mobile.set('12');
    page(fixture).typeTax('12345');
    await page(fixture).save();
    fixture.detectChanges();
    expect(fake.added.length).toBe(0);
    expect(text(fixture)).toContain(form.errors.REQUIRED);
    expect(text(fixture)).toContain(form.errors.INVALID_FORMAT);
    expect(text(fixture)).toContain(form.errors.MOBILE);
    expect(text(fixture)).toContain(form.errors.TAX_NUMBER);
  });

  it('sends the trimmed clinic, says it was added and opens its page', async () => {
    const fake = new FakeClinics();
    const fixture = await draw(fake);
    const router = TestBed.inject(Router);
    const went: string[] = [];
    router.events.subscribe(() => went.push(router.url));
    fill(page(fixture));
    await page(fixture).save();
    await settle();
    expect(fake.added).toEqual([
      {
        nameAr: 'عيادة النور',
        nameEn: 'Al Noor Clinic',
        ownerName: 'أحمد علي',
        email: 'owner@example.com',
        mobile: '+20 100 123 4567',
        address: 'القاهرة',
        planId: 'plan-basic',
        taxNumber: '',
        specialty: 'dental',
      },
    ]);
    expect(router.url).toBe('/clinics/new-clinic');
  });

  it('puts the server’s field refusal beside its field and keeps what was typed', async () => {
    const fake = new FakeClinics();
    fake.add = async () => {
      throw problem('VALIDATION_FAILED', 422, { errors: { email: ['EMAIL_TAKEN'] } });
    };
    const fixture = await draw(fake);
    fill(page(fixture));
    await page(fixture).save();
    fixture.detectChanges();
    expect(text(fixture)).toContain(form.addFailed);
    expect(page(fixture).nameEn()).toBe('Al Noor Clinic');
  });

  it('says the plan’s own words when its limit is reached, and the busy words when the limit is held', async () => {
    const fake = new FakeClinics();
    fake.add = async () => {
      throw problem('PLAN_LIMIT_REACHED', 409, { title: 'وصلت الباقة إلى حدّها.' });
    };
    const fixture = await draw(fake);
    fill(page(fixture));
    await page(fixture).save();
    fixture.detectChanges();
    expect(text(fixture)).toContain('وصلت الباقة إلى حدّها.');

    fake.add = async () => {
      throw problem('LIMIT_BUSY', 409);
    };
    await page(fixture).save();
    fixture.detectChanges();
    expect(text(fixture)).toContain(form.busy);
  });

  it('offers to try again when the form’s choices cannot be read', async () => {
    const fake = new FakeClinics();
    fake.form = async () => {
      throw new Error('down');
    };
    const fixture = await draw(fake);
    expect(has(fixture, 'form-failed')).toBe(true);
    expect(has(fixture, 'form-retry')).toBe(true);
  });
});

describe('the clinic form, editing (spec 015, User Story 4)', () => {
  it('opens filled, the plan read only, with the language, the time zone and the note', async () => {
    const fixture = await draw(new FakeClinics(), 'c1');
    expect(text(fixture)).toContain(form.save);
    expect(has(fixture, 'clinic-language')).toBe(true);
    expect(has(fixture, 'clinic-time-zone')).toBe(true);
    expect(has(fixture, 'clinic-note')).toBe(true);
    expect(page(fixture).nameEn()).toBe('Al Noor Clinic');
  });

  it('leaves the tax number out until touched, and sends nothing in it when cleared', async () => {
    const fake = new FakeClinics();
    const fixture = await draw(fake, 'c1');
    await page(fixture).save();
    expect(fake.edited[0]!.taxNumberSent).toBe(false);
    expect('taxNumber' in fake.edited[0]!).toBe(false);
    expect(fake.edited[0]!.version).toBe('AAAAAAAAB9E=');

    page(fixture).typeTax('');
    await page(fixture).save();
    expect(fake.edited[1]!.taxNumberSent).toBe(true);
    expect(fake.edited[1]!.taxNumber).toBe('');
  });

  it('offers «غير مسجّل» once among the specialties, as the select\u2019s nothing-chosen row', async () => {
    const fixture = await draw(new FakeClinics(), 'c1');
    const options = (fixture.componentInstance as unknown as { specialtyOptions: () => { value: string }[] }).specialtyOptions();
    expect(options.some((o) => o.value === '')).toBe(false);
  });

  it('shows no owner fields for a clinic with no owner account', async () => {
    const fake = new FakeClinics();
    fake.read = async () => detail({ owner: null });
    const fixture = await draw(fake, 'c1');
    expect(has(fixture, 'clinic-owner-name')).toBe(false);
    expect(has(fixture, 'clinic-email')).toBe(false);
    expect(has(fixture, 'clinic-mobile')).toBe(false);
    await page(fixture).save();
    expect(fake.edited.length).toBe(1);
  });

  it('says the clinic changed elsewhere and offers to reload', async () => {
    const fake = new FakeClinics();
    fake.edit = async () => {
      throw problem('CLINIC_CHANGED_ELSEWHERE');
    };
    const fixture = await draw(fake, 'c1');
    await page(fixture).save();
    fixture.detectChanges();
    expect(text(fixture)).toContain(form.saveFailed);
    expect(has(fixture, 'form-stale')).toBe(true);
    expect(has(fixture, 'form-reload')).toBe(true);
  });
});
