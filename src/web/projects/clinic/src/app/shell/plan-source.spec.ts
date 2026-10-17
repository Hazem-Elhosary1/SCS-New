import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { AuthService, PlanSource, StaffPlan } from 'core';
import ar from '../../../public/i18n/ar.json';
import en from '../../../public/i18n/en.json';
import { DESTINATIONS } from './destinations';
import { NotInPlanPage } from './pages';
import { ClinicPlanSource } from './plan-source';

const NOTHING: StaffPlan = { nameAr: null, nameEn: null, withheld: [], names: {} };

function plan(withheld: string[], names: StaffPlan['names'] = {}): StaffPlan {
  return { nameAr: 'باقة', nameEn: 'Plan', withheld, names };
}

function source(given: StaffPlan, refreshed: string[] = []): ClinicPlanSource {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      {
        provide: AuthService,
        useValue: {
          session: signal({ plan: given }),
          refreshSession: () => {
            refreshed.push('refresh');
            return Promise.resolve();
          },
        },
      },
    ],
  });
  return TestBed.inject(ClinicPlanSource);
}

describe('the destinations of plan features (spec 014, research R13)', () => {
  it('names the features of the BRD per page, and none for what every plan has', () => {
    const features = Object.fromEntries(
      DESTINATIONS.filter((place) => place.features).map((place) => [place.key, place.features]),
    );

    expect(features).toEqual({
      inbox: ['allow_whatsapp', 'allow_telegram'],
      'reports.financial': ['allow_analytics'],
      'reports.clinical': ['allow_analytics'],
      'reports.weekly': ['weekly_summary'],
      'reports.feedback': ['patient_feedback'],
      'administration.licences': ['licences_registry'],
      'data.consents': ['informed_consent'],
      'dental-lab': ['dental_lab'],
    });
  });
});

describe('ClinicPlanSource', () => {
  it('leaves nothing out when the plan withholds nothing', () => {
    const plans = source(NOTHING);

    expect(DESTINATIONS.some((place) => plans.excludes(place.key))).toBe(false);
  });

  it('leaves out a page whose feature is withheld, and never one with no feature', () => {
    const plans = source(plan(['allow_analytics']));

    expect(plans.excludes('reports.financial')).toBe(true);
    expect(plans.excludes('reports.clinical')).toBe(true);
    expect(plans.excludes('bookings')).toBe(false);
  });

  it('leaves out a page of any of two features only when both are withheld', () => {
    expect(source(plan(['allow_whatsapp'])).excludes('inbox')).toBe(false);
    expect(source(plan(['allow_whatsapp', 'allow_telegram'])).excludes('inbox')).toBe(true);
  });

  it('gives the catalogue name of what is withheld, in the language asked', () => {
    const plans = source(plan(['licences_registry'], { licences_registry: { ar: 'الرخص والتغطية', en: 'Licences & coverage' } }));

    expect(plans.featureName('administration.licences', 'ar')).toBe('الرخص والتغطية');
    expect(plans.featureName('administration.licences', 'en')).toBe('Licences & coverage');
    expect(plans.featureName('bookings', 'ar')).toBeNull();
  });

  it('reads the plan again through the session', async () => {
    const refreshed: string[] = [];
    await source(NOTHING, refreshed).refresh();

    expect(refreshed).toEqual(['refresh']);
  });
});

describe('NotInPlanPage', () => {
  async function card(destination: string, permissions: string[], names: StaffPlan['names'] = {}): Promise<HTMLElement> {
    TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [NotInPlanPage, TranslocoTestingModule.forRoot({
          langs: { ar, en },
          translocoConfig: { availableLangs: ['ar', 'en'], defaultLang: 'ar' },
          preloadLangs: true,
        })],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { data: { destination } } } },
        {
          provide: AuthService,
          useValue: { session: signal({ permissions, role: null, plan: plan([], names) }), status: signal('signed-out') },
        },
        { provide: PlanSource, useClass: ClinicPlanSource },
      ],
    }).compileComponents();
    const fixture = TestBed.createComponent(NotInPlanPage);
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  const text = (page: HTMLElement, id: string) => page.querySelector(`[data-testid="${id}"]`)?.textContent?.trim() ?? null;

  it('draws the inbox card with its words and the way to the subscription', async () => {
    const page = await card('inbox', ['inbox.view', 'subscription.view']);

    expect(text(page, 'empty-title')).toBe('الباقة الحاليّة لا تشمل قنوات المحادثة');
    expect(text(page, 'empty-text')).toBe(
      'قنوات المحادثة ليست في باقة العيادة الحاليّة. هذا حدّ الباقة لا نقصٌ في صلاحياتك، والباقة تُغيَّر من «الملف والاشتراك».',
    );
    expect(text(page, 'plan-link')).toBe('فتح الملف والاشتراك');
  });

  it('gives no link to whoever may not open the subscription, and says who changes the plan', async () => {
    const page = await card('reports.financial', ['reports.financial']);

    expect(text(page, 'empty-title')).toBe('الباقة الحاليّة لا تشمل التقارير');
    expect(text(page, 'empty-text')).toBe(
      'التقارير المالية والطبية ليست في باقة العيادة الحاليّة. هذا حدّ الباقة لا نقصٌ في صلاحياتك، والباقة تُغيَّر من «الملف والاشتراك»، ويُطلب ذلك من مالك العيادة.',
    );
    expect(page.querySelector('[data-testid="plan-link"]')).toBeNull();
  });

  it('names an undrawn page by the catalogue, whatever the name', async () => {
    const page = await card('administration.licences', ['admin.licences.view', 'subscription.view'], {
      licences_registry: { ar: 'الرخص والتغطية', en: 'Licences & coverage' },
    });

    expect(text(page, 'empty-title')).toBe('الباقة الحاليّة لا تشمل «الرخص والتغطية»');
    expect(text(page, 'empty-text')).toMatch(/^«الرخص والتغطية» ليست ميزة في باقة العيادة الحاليّة\./u);
  });
});
