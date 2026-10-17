import { TranslocoService } from '@jsverse/transloco';
import { OpsLanguage, opsNumber } from '../i18n/ops-format';

/** A plan's name in the page's language. */
export function planName(plan: { nameAr: string; nameEn: string }, language: OpsLanguage): string {
  return language === 'ar' ? plan.nameAr : plan.nameEn;
}

/** An amount grouped as the language groups, with up to two decimals, in Western digits. */
export function amount(value: number, language: OpsLanguage): string {
  return opsNumber(value, language, { maximumFractionDigits: 2 });
}

/** «499 ج.م» · "EGP 499" — or «25 دولار» · "USD 25". */
export function money(
  words: TranslocoService,
  value: number,
  currency: 'egp' | 'usd',
  language: OpsLanguage,
): string {
  return words.translate(`app.plans.plan.money.${currency}`, { amount: amount(value, language) });
}

/** «السعر الشهري» of a row: «<المبلغ> ج.م / شهريًا» · "EGP <amount> / mo". */
export function monthlyWords(words: TranslocoService, value: number, language: OpsLanguage): string {
  return words.translate('app.plans.plan.money.perMonth', { amount: amount(value, language) });
}

/**
 * «الحد الأقصى» (clarified: the BRD's single form): «مستخدم واحد», «حتى <ن> مستخدم», or
 * «مستخدمون غير محدودين» for no limit.
 */
export function usersWords(words: TranslocoService, limit: number | null, language: OpsLanguage): string {
  if (limit === null) {
    return words.translate('app.plans.plan.users.unlimited');
  }
  return limit === 1
    ? words.translate('app.plans.plan.users.one')
    : words.translate('app.plans.plan.users.upTo', { count: opsNumber(limit, language) });
}

/** «عيادة واحدة» · «<ن> عيادة» (§7-9) · "1 clinic" · "<n> clinics". */
export function clinicsCount(words: TranslocoService, count: number, language: OpsLanguage): string {
  return count === 1
    ? words.translate('app.plans.plan.clinics.one')
    : words.translate('app.plans.plan.clinics.many', { count: opsNumber(count, language) });
}

/** The first card's note: «باقة واحدة معطلة» · «<ن> باقة معطلة»; nothing when none is disabled. */
export function disabledNote(words: TranslocoService, count: number, language: OpsLanguage): string {
  if (count === 0) {
    return '';
  }
  return count === 1
    ? words.translate('app.plans.plan.cards.disabledOne')
    : words.translate('app.plans.plan.cards.disabledMany', { count: opsNumber(count, language) });
}

/** «+<ن> هذا الشهر» · "+<n> this month". */
export function newThisMonth(words: TranslocoService, count: number, language: OpsLanguage): string {
  return words.translate('app.plans.plan.cards.newThisMonth', { count: opsNumber(count, language) });
}

/**
 * The revenue's change against the end of last month: «+12٪ هذا الشهر», «−3٪ هذا الشهر» ·
 * "+12% this month"; nothing when there was no last month to compare with.
 */
export function changeWords(
  words: TranslocoService,
  now: number,
  last: number | null,
  language: OpsLanguage,
): string {
  if (last === null || last <= 0) {
    return '';
  }
  const share = Math.round(((now - last) / last) * 100);
  const sign = share > 0 ? '+' : share < 0 ? '−' : '';
  return words.translate('app.plans.plan.cards.change', {
    change: `${sign}${opsNumber(Math.abs(share), language)}`,
  });
}

/** «عرض <من>–<إلى> من <المجموع> باقات». */
export function planRange(
  words: TranslocoService,
  from: number,
  to: number,
  total: number,
  language: OpsLanguage,
): string {
  return words.translate('app.plans.plan.list.count', {
    from: opsNumber(from, language),
    to: opsNumber(to, language),
    total: opsNumber(total, language),
  });
}

const PLAN_WORDS = new Set(['الباقة', 'باقة', 'خطة', 'الخطة']);

/**
 * The avatar's two letters, as the frames draw them: in Arabic the first two letters of the
 * first word that is not «الباقة» or «باقة», without its «ال» («الباقة الأساسية» → «أس»); in
 * English the first letters of the first two words ("Basic Plan" → "BP").
 */
export function initials(name: string, language: OpsLanguage): string {
  const parts = name.trim().split(/\s+/u).filter((part) => part !== '');
  if (language === 'en') {
    const letters = parts
      .slice(0, 2)
      .map((part) => [...part][0] ?? '')
      .join('');
    return letters.toUpperCase();
  }
  const word = parts.find((part) => !PLAN_WORDS.has(part)) ?? parts[0] ?? '';
  const bare = word.startsWith('ال') && [...word].length > 3 ? word.slice(2) : word;
  return [...bare.replace(/[()«»"']/gu, '')].slice(0, 2).join('');
}

/** The monthly-counted features: their unit already says «/شهر», so the detail drops «حتى». */
export const MONTHLY_FEATURES = [
  'message_credits',
  'ai_xray_analysis',
  'ai_voice_notes',
  'teleconsult_minutes',
] as const;

/**
 * A feature's value in the detail's «ميزات الباقة» (User Story 4 scenario 3): «✓» for a switch,
 * «بلا حدّ» for no limit, «<ن> <الوحدة>» for a monthly limit, «حتى <ن> <الوحدة>» for another.
 */
export function includedValue(
  words: TranslocoService,
  feature: { key: string; kind: 'switch' | 'limit'; unitAr: string | null; unitEn: string | null },
  limit: number | null,
  language: OpsLanguage,
): string {
  if (feature.kind === 'switch') {
    return '✓';
  }
  if (limit === null) {
    return words.translate('app.plans.plan.detail.noLimit');
  }
  const unit = (language === 'ar' ? feature.unitAr : feature.unitEn) ?? '';
  const count = opsNumber(limit, language);
  return (MONTHLY_FEATURES as readonly string[]).includes(feature.key)
    ? words.translate('app.plans.plan.detail.perMonthLimit', { count, unit })
    : words.translate('app.plans.plan.detail.upTo', { count, unit });
}
