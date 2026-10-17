import { TestBed } from '@angular/core/testing';
import { TranslocoService, TranslocoTestingModule } from '@jsverse/transloco';
import ar from '../../../public/i18n/ar.json';
import en from '../../../public/i18n/en.json';
import { hasEasternDigit } from '../i18n/ops-format';
import {
  changeWords,
  clinicsCount,
  disabledNote,
  includedValue,
  initials,
  money,
  monthlyWords,
  newThisMonth,
  planRange,
  usersWords,
} from './plan-words';

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

const limit = (key: string, unitAr: string, unitEn: string) => ({
  key,
  kind: 'limit' as const,
  unitAr,
  unitEn,
});

describe('the plans’ words (contracts/plans-screens.md)', () => {
  it('writes the users’ limit in the BRD’s single form (clarified)', () => {
    const t = words('ar');
    expect(usersWords(t, 1, 'ar')).toBe('مستخدم واحد');
    expect(usersWords(t, 3, 'ar')).toBe('حتى 3 مستخدم');
    expect(usersWords(t, 1500, 'ar')).toBe('حتى 1,500 مستخدم');
    expect(usersWords(t, null, 'ar')).toBe('مستخدمون غير محدودين');
    const e = words('en');
    expect(usersWords(e, 1, 'en')).toBe('1 user');
    expect(usersWords(e, 10, 'en')).toBe('Up to 10 users');
    expect(usersWords(e, null, 'en')).toBe('Unlimited users');
  });

  it('writes money with separators and up to two decimals, in Western digits', () => {
    const t = words('ar');
    expect(money(t, 4990, 'egp', 'ar')).toBe('4,990 ج.م');
    expect(money(t, 0.5, 'egp', 'ar')).toBe('0.5 ج.م');
    expect(money(t, 25, 'usd', 'ar')).toBe('25 دولار');
    expect(monthlyWords(t, 249, 'ar')).toBe('249 ج.م / شهريًا');
    expect(hasEasternDigit(money(t, 46750, 'egp', 'ar'))).toBe(false);
    const e = words('en');
    expect(money(e, 46750, 'egp', 'en')).toBe('EGP 46,750');
    expect(money(e, 12.25, 'usd', 'en')).toBe('USD 12.25');
    expect(monthlyWords(e, 499, 'en')).toBe('EGP 499 / mo');
  });

  it('counts clinics as §7-9 does', () => {
    const t = words('ar');
    expect(clinicsCount(t, 1, 'ar')).toBe('عيادة واحدة');
    expect(clinicsCount(t, 0, 'ar')).toBe('0 عيادة');
    expect(clinicsCount(t, 64, 'ar')).toBe('64 عيادة');
    const e = words('en');
    expect(clinicsCount(e, 1, 'en')).toBe('1 clinic');
    expect(clinicsCount(e, 18, 'en')).toBe('18 clinics');
  });

  it('writes the cards’ notes, and none where there is nothing to say', () => {
    const t = words('ar');
    expect(disabledNote(t, 0, 'ar')).toBe('');
    expect(disabledNote(t, 1, 'ar')).toBe('باقة واحدة معطلة');
    expect(disabledNote(t, 2, 'ar')).toBe('2 باقة معطلة');
    expect(newThisMonth(t, 8, 'ar')).toBe('+8 هذا الشهر');
    expect(changeWords(t, 112, 100, 'ar')).toBe('+12٪ هذا الشهر');
    expect(changeWords(t, 97, 100, 'ar')).toBe('−3٪ هذا الشهر');
    expect(changeWords(t, 100, null, 'ar')).toBe('');
    const e = words('en');
    expect(disabledNote(e, 1, 'en')).toBe('1 plan disabled');
    expect(changeWords(e, 112, 100, 'en')).toBe('+12% this month');
  });

  it('counts the pages as the table footer does', () => {
    expect(planRange(words('ar'), 1, 4, 4, 'ar')).toBe('عرض 1–4 من 4 باقات');
    expect(planRange(words('en'), 9, 16, 20, 'en')).toBe('Showing 9–16 of 20 plans');
  });

  it('takes the avatar’s letters as the frames draw them', () => {
    expect(initials('الباقة الأساسية', 'ar')).toBe('أس');
    expect(initials('الباقة الاحترافية', 'ar')).toBe('اح');
    expect(initials('الباقة المؤسسية', 'ar')).toBe('مؤ');
    expect(initials('باقة تجريبية (قديمة)', 'ar')).toBe('تج');
    expect(initials('Basic Plan', 'en')).toBe('BP');
    expect(initials('Legacy Trial Plan', 'en')).toBe('LT');
    expect(initials('pro', 'en')).toBe('P');
  });

  it('writes a feature’s value in the detail', () => {
    const t = words('ar');
    expect(includedValue(t, { key: 'allow_whatsapp', kind: 'switch', unitAr: null, unitEn: null }, null, 'ar')).toBe('✓');
    expect(includedValue(t, limit('max_users', 'مستخدم', 'users'), 10, 'ar')).toBe('حتى 10 مستخدم');
    expect(includedValue(t, limit('max_users', 'مستخدم', 'users'), null, 'ar')).toBe('بلا حدّ');
    expect(includedValue(t, limit('message_credits', 'رسالة/شهر', 'msgs / mo'), 1000, 'ar')).toBe('1,000 رسالة/شهر');
    const e = words('en');
    expect(includedValue(e, limit('max_doctors', 'طبيب', 'doctors'), 5, 'en')).toBe('Up to 5 doctors');
    expect(includedValue(e, limit('ai_xray_analysis', 'تحليل/شهر', 'scans / mo'), 200, 'en')).toBe('200 scans / mo');
  });
});
