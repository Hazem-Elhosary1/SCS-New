import { TestBed } from '@angular/core/testing';
import { TranslocoService, TranslocoTestingModule } from '@jsverse/transloco';
import ar from '../../../public/i18n/ar.json';
import en from '../../../public/i18n/en.json';
import { hasEasternDigit } from '../i18n/ops-format';
import {
  clinicName,
  daysUntil,
  detailMark,
  doctorsAndBranches,
  endsIn,
  listMark,
  planWords,
  statusIntent,
  statusWord,
  trialLength,
} from './clinic-words';

function words(language: 'ar' | 'en'): TranslocoService {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [
      TranslocoTestingModule.forRoot({
        langs: { ar, en },
        translocoConfig: { availableLangs: ['ar', 'en'], defaultLang: language },
        preloadLangs: true,
      }),
    ],
  });
  return TestBed.inject(TranslocoService);
}

const noor = { nameAr: 'عيادة النور', nameEn: 'Al Noor Clinic' };
const DAY = 86_400_000;
const NOW = Date.parse('2026-10-17T09:00:00Z');

describe('the clinics’ words (contracts/clinics-screens.md)', () => {
  it('names the clinic in the page’s language', () => {
    expect(clinicName(noor, 'ar')).toBe('عيادة النور');
    expect(clinicName(noor, 'en')).toBe('Al Noor Clinic');
  });

  it('marks a row with the name’s own letter in Arabic and two Latin letters in English', () => {
    expect(listMark(noor, 'ar')).toBe('ن');
    expect(listMark({ nameAr: 'مركز الشفاء', nameEn: 'Shifa Center' }, 'ar')).toBe('ش');
    expect(listMark({ nameAr: 'عيادة', nameEn: 'Clinic' }, 'ar')).toBe('ع');
    expect(listMark(noor, 'en')).toBe('AN');
    expect(listMark({ nameAr: 'س', nameEn: 'smile' }, 'en')).toBe('S');
  });

  it('marks the detail with Latin letters in both languages, falling back to the Arabic letter', () => {
    expect(detailMark(noor)).toBe('AN');
    expect(detailMark({ nameAr: 'عيادة النور', nameEn: '' })).toBe('ن');
  });

  it('gives the status in the page’s language with its badge intent', () => {
    const trial = { code: 'trial', ar: 'تجربة', en: 'Trial', tone: 'info' as const };
    expect(statusWord(trial, 'ar')).toBe('تجربة');
    expect(statusWord(trial, 'en')).toBe('Trial');
    expect(statusIntent(trial)).toBe('info');
  });

  it('counts the days left up, never below nought', () => {
    expect(daysUntil(new Date(NOW + 3 * DAY + 2 * 3_600_000).toISOString(), NOW)).toBe(4);
    expect(daysUntil(new Date(NOW + 14 * DAY).toISOString(), NOW)).toBe(14);
    expect(daysUntil(new Date(NOW - DAY).toISOString(), NOW)).toBe(0);
  });

  it('says when a trial ends, in Arabic’s forms and in English', () => {
    const later = (days: number) => new Date(NOW + days * DAY).toISOString();
    const arabic = words('ar');
    expect(endsIn(arabic, later(1), NOW, 'ar')).toBe('خلال يوم واحد');
    expect(endsIn(arabic, later(2), NOW, 'ar')).toBe('خلال يومين');
    expect(endsIn(arabic, later(4), NOW, 'ar')).toBe('خلال 4 أيام');
    expect(hasEasternDigit(endsIn(arabic, later(14), NOW, 'ar'))).toBe(false);
    const english = words('en');
    expect(endsIn(english, later(1), NOW, 'en')).toBe('in 1 day');
    expect(endsIn(english, later(4), NOW, 'en')).toBe('in 4 days');
  });

  it('writes the trial’s length as the frames do', () => {
    expect(trialLength(words('ar'), 14, 'ar')).toBe('تجربة 14 يومًا');
    expect(trialLength(words('ar'), 7, 'ar')).toBe('تجربة 7 أيام');
    expect(trialLength(words('ar'), 2, 'ar')).toBe('تجربة يومين');
    expect(trialLength(words('en'), 14, 'en')).toBe('14-day trial');
  });

  it('names the plan once, whether or not its name already says it is a plan', () => {
    expect(planWords(words('ar'), { nameAr: 'الاحترافية', nameEn: 'Professional' }, 'ar')).toBe('باقة الاحترافية');
    expect(planWords(words('ar'), { nameAr: 'الباقة الأساسية', nameEn: 'Basic Plan' }, 'ar')).toBe('الباقة الأساسية');
    expect(planWords(words('ar'), { nameAr: 'باقة الجولة', nameEn: 'Walk Plan' }, 'ar')).toBe('باقة الجولة');
    expect(planWords(words('en'), { nameAr: 'الاحترافية', nameEn: 'Professional' }, 'en')).toBe('Professional plan');
    expect(planWords(words('en'), { nameAr: 'الباقة الأساسية', nameEn: 'Basic Plan' }, 'en')).toBe('Basic Plan');
  });

  it('counts the doctors and the branches', () => {
    expect(doctorsAndBranches(words('ar'), 0, 1, 'ar')).toBe('0 أطباء · فرع واحد');
    expect(doctorsAndBranches(words('en'), 0, 1, 'en')).toBe('0 doctors · 1 branch');
    expect(doctorsAndBranches(words('en'), 2, 3, 'en')).toBe('2 doctors · 3 branches');
  });
});
