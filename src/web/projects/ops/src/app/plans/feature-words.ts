import { TranslocoService } from '@jsverse/transloco';
import { OpsLanguage, opsNumber } from '../i18n/ops-format';
import { countWords } from '../operators/count-words';
import { FeatureRow, SPECIALTY_DECIDED } from './features.service';

/** The feature's name in the page's language. */
export function featureName(
  feature: Pick<FeatureRow, 'nameAr' | 'nameEn'>,
  language: OpsLanguage,
): string {
  return language === 'ar' ? feature.nameAr : feature.nameEn;
}

/** The feature's unit in the page's language; empty for a switch. */
export function featureUnit(
  feature: Pick<FeatureRow, 'unitAr' | 'unitEn'>,
  language: OpsLanguage,
): string {
  return (language === 'ar' ? feature.unitAr : feature.unitEn) ?? '';
}

/**
 * «النوع» of a row (spec 012, User Story 1 scenario 4): «مفتاح», «حدّ · <الوحدة>», «مفتاح ·
 * للأسنان», or «يتبع التخصص» for the one feature the specialty decides.
 */
export function featureType(
  words: TranslocoService,
  feature: FeatureRow,
  language: OpsLanguage,
): string {
  if (feature.key === SPECIALTY_DECIDED) {
    return words.translate('app.plans.type.followsSpecialty');
  }
  const parts = [words.translate(`app.plans.type.${feature.kind}`)];
  if (feature.kind === 'limit') {
    parts.push(featureUnit(feature, language));
  }
  if (feature.specialty !== null) {
    parts.push(words.translate(`app.plans.forSpecialty.${feature.specialty}`));
  }
  return parts.filter((part) => part !== '').join(' · ');
}

/** «في الباقات»: «لا تُباع بعد», «باقة واحدة», «باقتان», «<ن> باقات» — Western digits always. */
export function inPlans(words: TranslocoService, count: number, language: OpsLanguage): string {
  return count === 0
    ? words.translate('app.plans.inPlans.none')
    : countWords(words, 'app.plans.inPlans', count, language);
}

/** «<ن> عيادة» of a plan in «أين تُستعمل». */
export function clinicsWords(
  words: TranslocoService,
  count: number,
  language: OpsLanguage,
): string {
  return countWords(words, 'app.plans.clinics', count, language);
}

/** The cards' note «<ن> مجموعات». */
export function groupsWords(words: TranslocoService, count: number, language: OpsLanguage): string {
  return countWords(words, 'app.plans.cards.groupsNote', count, language);
}

/** «عرض <من>–<إلى> من <المجموع> ميزة». */
export function rangeWords(
  words: TranslocoService,
  from: number,
  to: number,
  total: number,
  language: OpsLanguage,
): string {
  return words.translate('app.plans.list.count', {
    from: opsNumber(from, language),
    to: opsNumber(to, language),
    total: opsNumber(total, language),
  });
}
