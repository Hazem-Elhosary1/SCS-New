import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { TranslocoService, TranslocoTestingModule } from '@jsverse/transloco';
import { LanguageService, ScsProblemError } from 'core';
import ar from '../../../public/i18n/ar.json';
import en from '../../../public/i18n/en.json';
import { OpsAccess } from '../access/ops-access.service';
import { FeatureCataloguePage } from './feature-catalogue.page';
import { FeatureFormPage } from './feature-form.page';
import { featureType, inPlans, rangeWords } from './feature-words';
import { FeatureRow, FeaturesService, FeaturesView, WhereUsed } from './features.service';
import { RetireFeatureDialog } from './retire-feature.dialog';
import { WhereUsedDialog } from './where-used.dialog';

const settle = () => new Promise((resolve) => setTimeout(resolve));

function feature(key: string, extra: Partial<FeatureRow> = {}): FeatureRow {
  return {
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
    status: 'upcoming',
    isSold: true,
    isRetired: false,
    sortOrder: 10,
    monthly: false,
    plans: 0,
    version: 1,
    ...extra,
  };
}

const view = (items: FeatureRow[], manage = true): FeaturesView => ({
  items,
  cards: {
    total: items.length,
    groups: new Set(items.map((i) => i.group)).size,
    built: 0,
    upcoming: items.length,
    soldInPlans: 0,
    plans: 0,
  },
  can: { manage },
});

const refusal = (status: number, code: string) =>
  new ScsProblemError({ type: 'x', title: code, status, code, traceId: 'a'.repeat(32) }, false);

/** Stands in for the platform's catalogue: answers what each test says, and remembers what was sent. */
class FakeFeatures {
  readonly sent: unknown[] = [];
  list: () => Promise<FeaturesView> = async () => view([]);
  read: (key: string) => Promise<FeatureRow> = async (key) => feature(key);
  add: (body: unknown) => Promise<string> = async (body) => {
    this.sent.push(body);
    return 'k';
  };
  edit: (key: string, body: unknown) => Promise<void> = async (_key, body) => {
    this.sent.push(body);
  };
  whereUsed: () => Promise<WhereUsed> = async () => ({ items: [], total: 0 });
}

function configure(
  language: 'ar' | 'en',
  codes: string[],
  fake: FakeFeatures,
  key: string | null = null,
) {
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
      {
        provide: LanguageService,
        useValue: { language: spoken, use: (next: 'ar' | 'en') => spoken.set(next) },
      },
      { provide: OpsAccess, useValue: { can: (code: string) => held().has(code) } },
      { provide: FeaturesService, useValue: fake },
      {
        provide: ActivatedRoute,
        useValue: { snapshot: { paramMap: convertToParamMap(key === null ? {} : { key }) } },
      },
    ],
  });
}

const text = (fixture: ComponentFixture<unknown>, testId: string) =>
  (fixture.nativeElement as HTMLElement)
    .querySelector(`[data-testid="${testId}"]`)
    ?.textContent?.trim() ?? null;

const all = (fixture: ComponentFixture<unknown>, testId: string) =>
  [...(fixture.nativeElement as HTMLElement).querySelectorAll(`[data-testid="${testId}"]`)].map(
    (node) => node.textContent?.trim() ?? '',
  );

describe('the feature catalogue’s words', () => {
  beforeEach(() => configure('ar', [], new FakeFeatures()));

  it('writes the type as the frame does, in each language', () => {
    const words = TestBed.inject(TranslocoService);
    expect(featureType(words, feature('a'), 'ar')).toBe('مفتاح');
    expect(
      featureType(
        words,
        feature('b', { kind: 'limit', unitAr: 'تحليل/شهر', unitEn: 'scans / mo' }),
        'ar',
      ),
    ).toBe('حدّ · تحليل/شهر');
    expect(featureType(words, feature('dental_chart', { specialty: 'dental' }), 'ar')).toBe(
      'مفتاح · للأسنان',
    );
    expect(featureType(words, feature('specialty_exam_modules', { isSold: false }), 'ar')).toBe(
      'يتبع التخصص',
    );
  });

  it('counts the plans and the range in Western digits', () => {
    const words = TestBed.inject(TranslocoService);
    expect([0, 1, 2, 3, 11].map((count) => inPlans(words, count, 'ar'))).toEqual([
      'لا تُباع بعد',
      'باقة واحدة',
      'باقتان',
      '3 باقات',
      '11 باقة',
    ]);
    expect(rangeWords(words, 1, 13, 35, 'ar')).toBe('عرض 1–13 من 35 ميزة');
  });
});

describe('the feature catalogue page', () => {
  async function open(language: 'ar' | 'en', codes: string[], fake: FakeFeatures) {
    configure(language, codes, fake);
    const fixture = TestBed.createComponent(FeatureCataloguePage);
    fixture.detectChanges();
    await settle();
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture;
  }

  it('shows thirteen rows a page, by the catalogue’s order, with the count of the whole', async () => {
    const fake = new FakeFeatures();
    fake.list = async () =>
      view(Array.from({ length: 35 }, (_, i) => feature(`f_${String(i).padStart(2, '0')}`)));
    const fixture = await open('ar', ['billing.view'], fake);

    expect(all(fixture, 'feature-key')).toHaveLength(13);
    expect(all(fixture, 'feature-key')[0]).toBe('f_00');
    expect(text(fixture, 'card-total')).toContain('35');
  });

  it('gives a viewer no add button and a manager one', async () => {
    const viewer = await open('ar', ['billing.view'], new FakeFeatures());
    expect(text(viewer, 'add-feature')).toBeNull();
    TestBed.resetTestingModule();
    const manager = await open('ar', ['billing.view', 'billing.managePlans'], new FakeFeatures());
    expect(text(manager, 'add-feature')).toContain('إضافة ميزة');
  });

  it('says it could not load, with the console’s retry word', async () => {
    const fake = new FakeFeatures();
    fake.list = async () => {
      throw refusal(503, 'SERVICE_UNAVAILABLE');
    };
    const fixture = await open('ar', ['billing.view'], fake);
    expect(text(fixture, 'features-failed')).toContain('تعذّر تحميل الكتالوج.');
    expect(text(fixture, 'features-retry')).toBe('حاول مرة أخرى');
  });

  it('marks a retired feature in its row, in English too', async () => {
    const fake = new FakeFeatures();
    fake.list = async () =>
      view([feature('a', { isRetired: true }), feature('b', { status: 'built' })]);
    const fixture = await open('en', ['billing.view'], fake);
    expect(all(fixture, 'feature-retired')).toEqual(['Retired']);
    expect(all(fixture, 'feature-status')).toEqual(['Planned', 'Built']);
    expect(all(fixture, 'feature-plans')).toEqual(['Not sold yet', 'Not sold yet']);
  });
});

describe('the feature form', () => {
  async function open(fake: FakeFeatures, key: string | null = null) {
    configure('ar', ['billing.view', 'billing.managePlans'], fake, key);
    const fixture = TestBed.createComponent(FeatureFormPage);
    fixture.detectChanges();
    await settle();
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture;
  }

  const set = (
    fixture: ComponentFixture<FeatureFormPage>,
    values: Partial<Record<string, unknown>>,
  ) => {
    const page = fixture.componentInstance as unknown as Record<
      string,
      { set: (value: unknown) => void }
    >;
    for (const [name, value] of Object.entries(values)) {
      page[name].set(value);
    }
    fixture.detectChanges();
  };

  const save = async (fixture: ComponentFixture<FeatureFormPage>) => {
    await (fixture.componentInstance as unknown as { save: () => Promise<void> }).save();
    await settle();
    fixture.detectChanges();
  };

  it('shows the units and «بدون هذه القدرة» for a limit only', async () => {
    const fixture = await open(new FakeFeatures());
    expect(text(fixture, 'field-unit-ar')).toBeNull();
    set(fixture, { kind: 'limit' });
    expect(fixture.nativeElement.querySelector('[data-testid="field-unit-ar"]')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('[data-testid="field-baseline"]')).not.toBeNull();
  });

  it('refuses before asking: the names, the key’s shape and a limit’s units', async () => {
    const fake = new FakeFeatures();
    const fixture = await open(fake);
    set(fixture, { key: 'Bad-Key', kind: 'limit' });
    await save(fixture);

    const page = fixture.nativeElement as HTMLElement;
    expect(page.textContent).toContain('هذا الحقل مطلوب.');
    expect(page.textContent).toContain('حروف إنجليزية صغيرة وأرقام وشرطات سفلية، ويبدأ بحرف.');
    expect(page.textContent).toContain('الحدّ يحتاج وحدة بالعربية والإنجليزية: ماذا يعدّ؟');
    expect(fake.sent).toEqual([]);
  });

  it('sends a switch without units or baseline, and returns to the catalogue', async () => {
    const fake = new FakeFeatures();
    const fixture = await open(fake);
    const router = TestBed.inject(Router);
    const went = vi.spyOn(router, 'navigateByUrl');
    set(fixture, {
      nameAr: 'قناة',
      nameEn: 'Channel',
      key: 'new_channel',
      unitAr: 'باق',
      baseline: '4',
    });
    await save(fixture);

    expect(fake.sent).toEqual([
      expect.objectContaining({
        key: 'new_channel',
        kind: 'switch',
        unitAr: null,
        unitEn: null,
        baseline: null,
        isSold: true,
        status: 'upcoming',
        group: 'core',
      }),
    ]);
    expect(went).toHaveBeenCalledWith('/plans/features');
  });

  it('says a key in use at the top and at the key, and keeps what was typed', async () => {
    const fake = new FakeFeatures();
    fake.add = async () => {
      throw refusal(409, 'FEATURE_KEY_TAKEN');
    };
    const fixture = await open(fake);
    set(fixture, { nameAr: 'قناة', nameEn: 'Channel', key: 'max_users' });
    await save(fixture);

    expect(text(fixture, 'form-problem')).toBe('تعذّر حفظ الميزة.');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('هذا المفتاح لميزة أخرى.');
    expect((fixture.componentInstance as unknown as { key: () => string }).key()).toBe('max_users');
  });

  it('edits with the key fixed and its version, and locks «تُباع» on a retired feature', async () => {
    const fake = new FakeFeatures();
    fake.read = async (key) => feature(key, { isRetired: true, version: 4 });
    const fixture = await open(fake, 'allow_x');

    const key = fixture.nativeElement.querySelector(
      'input[data-testid="field-key"]',
    ) as HTMLInputElement | null;
    expect(key?.readOnly).toBe(true);
    expect(text(fixture, 'form-title')).toBe('تعديل ميزة');

    await save(fixture);
    expect(fake.sent).toEqual([expect.objectContaining({ version: 4, isSold: true })]);
    expect('key' in (fake.sent[0] as object)).toBe(false);
  });

  it('says plainly when the feature of the address does not exist', async () => {
    const fake = new FakeFeatures();
    fake.read = async () => {
      throw refusal(404, 'NOT_FOUND');
    };
    const fixture = await open(fake, 'gone_feature');
    expect(text(fixture, 'form-missing')).toContain('لم تعد هذه الميزة موجودة.');
  });

  it('says plainly when the feature went missing between opening and saving', async () => {
    const fake = new FakeFeatures();
    fake.edit = async () => {
      throw refusal(404, 'NOT_FOUND');
    };
    const fixture = await open(fake, 'allow_x');
    await save(fixture);
    expect(text(fixture, 'form-problem')).toBe('لم تعد هذه الميزة موجودة.');
    expect(fake.sent).toEqual([]);
  });
});

describe('the catalogue’s addresses', () => {
  it('reads, writes, retires and sells again at the addresses of the contract', async () => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    const service = TestBed.inject(FeaturesService);
    const http = TestBed.inject(HttpTestingController);

    const listed = service.list();
    http.expectOne({ method: 'GET', url: '/v1/ops/features' }).flush(view([]));
    await listed;

    const used = service.whereUsed('max_users');
    http
      .expectOne({ method: 'GET', url: '/v1/ops/features/max_users/plans' })
      .flush({ items: [], total: 0 });
    await used;

    const added = service.add({ key: 'k_a' } as never);
    http.expectOne({ method: 'POST', url: '/v1/ops/features' }).flush({ key: 'k_a' });
    expect(await added).toBe('k_a');

    const edited = service.edit('k_a', {} as never);
    http
      .expectOne({ method: 'PUT', url: '/v1/ops/features/k_a' })
      .flush(null, { status: 204, statusText: 'No Content' });
    await edited;

    const retired = service.retire('k_a');
    http
      .expectOne({ method: 'POST', url: '/v1/ops/features/k_a/retire' })
      .flush(null, { status: 204, statusText: 'No Content' });
    await retired;

    const resumed = service.resume('k_a');
    http
      .expectOne({ method: 'POST', url: '/v1/ops/features/k_a/resume' })
      .flush(null, { status: 204, statusText: 'No Content' });
    await resumed;

    http.verify();
  });
});

describe('the catalogue’s dialogs', () => {
  beforeAll(() => {
    // The test DOM has the dialog element but not its two methods.
    const proto = HTMLDialogElement.prototype as HTMLDialogElement & {
      showModal?: unknown;
      close?: unknown;
    };
    proto.showModal ??= function (this: HTMLDialogElement) {
      this.setAttribute('open', '');
    };
    proto.close ??= function (this: HTMLDialogElement) {
      this.removeAttribute('open');
      this.dispatchEvent(new Event('close'));
    };
  });

  it('names the plans that keep a feature before it is retired, with their clinics', async () => {
    const fake = new FakeFeatures();
    fake.whereUsed = async () => ({
      items: [
        { id: 'p-1', nameAr: 'الباقة الاحترافية', nameEn: 'Professional Plan', state: 'active' as const, clinics: 64 },
        { id: 'p-2', nameAr: 'الباقة المؤسسية', nameEn: 'Enterprise Plan', state: 'active' as const, clinics: 12 },
      ],
      total: 76,
    });
    configure('ar', ['billing.managePlans'], fake);
    const fixture = TestBed.createComponent(RetireFeatureDialog);
    fixture.componentRef.setInput('feature', feature('max_users', { plans: 2 }));
    fixture.detectChanges();
    await settle();
    fixture.detectChanges();

    const words = document.querySelector('[data-testid="retire-warning"]')?.textContent ?? '';
    expect(words).toContain('الباقة الاحترافية والباقة المؤسسية');
    expect(words).toContain('76 عيادة');
  });

  it('says plainly that no plan includes a feature yet', async () => {
    configure('en', ['billing.view'], new FakeFeatures());
    const fixture = TestBed.createComponent(WhereUsedDialog);
    fixture.componentRef.setInput('feature', feature('max_users'));
    fixture.detectChanges();
    await settle();
    fixture.detectChanges();
    expect(document.querySelector('[data-testid="where-used-none"]')?.textContent?.trim()).toBe(
      'No plan includes it yet.',
    );
  });
});

describe('the feature form and the platform’s field refusals', () => {
  it('says a field the platform refused beside it, and the failure at the top', async () => {
    const fake = new FakeFeatures();
    fake.add = async () => {
      throw new ScsProblemError(
        {
          type: 'x',
          title: 'VALIDATION_FAILED',
          status: 400,
          code: 'VALIDATION_FAILED',
          traceId: 'a'.repeat(32),
          errors: { nameEn: ['TOO_LONG'], key: ['INVALID'] },
        } as never,
        false,
      );
    };
    configure('ar', ['billing.view', 'billing.managePlans'], fake);
    const fixture = TestBed.createComponent(FeatureFormPage);
    fixture.detectChanges();
    const page = fixture.componentInstance as unknown as Record<
      string,
      { set: (value: unknown) => void }
    > & { save: () => Promise<void> };
    page['nameAr'].set('قناة');
    page['nameEn'].set('Channel');
    page['key'].set('ok_key');
    fixture.detectChanges();
    await page.save();
    await settle();
    fixture.detectChanges();

    const words = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text(fixture, 'form-problem')).toBe('تعذّر حفظ الميزة.');
    expect(words).toContain('النص أطول من المسموح.');
    expect(words).toContain('حروف إنجليزية صغيرة وأرقام وشرطات سفلية، ويبدأ بحرف.');
  });
});

describe('the catalogue with nothing in it, and the form’s own checks', () => {
  it('says the catalogue holds no feature yet, and offers no search (FR-007)', async () => {
    configure('ar', ['billing.view'], new FakeFeatures());
    const fixture = TestBed.createComponent(FeatureCataloguePage);
    fixture.detectChanges();
    await settle();
    fixture.detectChanges();
    expect(text(fixture, 'features-empty')).toContain('لا ميزات بعد');
    expect((fixture.nativeElement as HTMLElement).querySelector('scs-list-toolbar')).toBeNull();
  });

  async function form(values: Record<string, unknown>) {
    const fake = new FakeFeatures();
    configure('ar', ['billing.view', 'billing.managePlans'], fake);
    const fixture = TestBed.createComponent(FeatureFormPage);
    fixture.detectChanges();
    const page = fixture.componentInstance as unknown as Record<
      string,
      { set: (value: unknown) => void }
    > & {
      save: () => Promise<void>;
    };
    const base: Record<string, unknown> = { nameAr: 'قناة', nameEn: 'Channel', key: 'ok_key' };
    for (const [name, value] of Object.entries({ ...base, ...values })) {
      page[name].set(value);
    }
    fixture.detectChanges();
    await page.save();
    await settle();
    fixture.detectChanges();
    return { words: (fixture.nativeElement as HTMLElement).textContent ?? '', fake };
  }

  const keyWords = 'حروف إنجليزية صغيرة وأرقام وشرطات سفلية، ويبدأ بحرف.';
  const baselineWords = 'رقم صحيح من 0 إلى 1,000,000، أو فارغ لـ«بلا حد».';
  const tooLong = 'النص أطول من المسموح.';

  it.each([
    [{ key: 'a' }, keyWords],
    [{ key: 'a' + 'b'.repeat(64) }, keyWords],
    [{ kind: 'limit', unitAr: 'وحدة', unitEn: 'unit', baseline: '-1' }, baselineWords],
    [{ kind: 'limit', unitAr: 'وحدة', unitEn: 'unit', baseline: '1000001' }, baselineWords],
    [{ kind: 'limit', unitAr: 'وحدة', unitEn: 'unit', baseline: 'abc' }, baselineWords],
    [{ nameEn: 'n'.repeat(121) }, tooLong],
    [{ kind: 'limit', unitAr: 'و'.repeat(41), unitEn: 'unit' }, tooLong],
    [{ descriptionAr: 'و'.repeat(501) }, tooLong],
  ])('refuses %o beside its field and sends nothing (FR-015)', async (values, said) => {
    const { words, fake } = await form(values);
    expect(words).toContain(said);
    expect(fake.sent).toEqual([]);
  });
});

describe('the baseline field’s words (BRD §6-14)', () => {
  it('says «بلا حدّ» as its placeholder and §6-14’s hint', async () => {
    configure('ar', ['billing.view', 'billing.managePlans'], new FakeFeatures());
    const fixture = TestBed.createComponent(FeatureFormPage);
    fixture.detectChanges();
    (fixture.componentInstance as unknown as { kind: { set: (v: string) => void } }).kind.set(
      'limit',
    );
    fixture.detectChanges();
    const input = fixture.nativeElement.querySelector(
      'input[data-testid="field-baseline"]',
    ) as HTMLInputElement;
    expect(input.getAttribute('placeholder')).toBe('بلا حدّ');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain(
      'ما تحصل عليه العيادة حين لا تشمل باقتها هذا الحدّ. اتركه فارغًا لبلا حدّ، أو 1 لواحد، أو 0 لِلا شيء.',
    );
  });
});
