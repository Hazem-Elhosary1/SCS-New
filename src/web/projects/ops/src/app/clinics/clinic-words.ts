import { TranslocoService } from '@jsverse/transloco';
import { OpsLanguage, opsNumber } from '../i18n/ops-format';
import { countWords } from '../operators/count-words';
import { ClinicStatus } from './clinics.service';

/** Words a clinic's Arabic name starts with that say what it is, not which it is. */
const KIND_WORDS = new Set(['عيادة', 'عيادات', 'مركز', 'مجمع', 'مستشفى', 'د.', 'دكتور']);

/** The name in the page's language. */
export function clinicName(clinic: { nameAr: string; nameEn: string }, language: OpsLanguage): string {
  return language === 'ar' ? clinic.nameAr : clinic.nameEn;
}

/**
 * The list's mark, as its frames draw it (`606:776`, `599:680`): in Arabic the first letter of the
 * name's own word («عيادة النور» → «ن»); in English the first letters of two words («AN»).
 */
export function listMark(clinic: { nameAr: string; nameEn: string }, language: OpsLanguage): string {
  return language === 'ar' ? arabicLetter(clinic.nameAr) : latinLetters(clinic.nameEn);
}

/** The detail's mark: Latin letters in both languages, as its frames draw it (the owner's answer of 17 Oct 2026). */
export function detailMark(clinic: { nameAr: string; nameEn: string }): string {
  const latin = latinLetters(clinic.nameEn);
  return latin === '' ? arabicLetter(clinic.nameAr) : latin;
}

function latinLetters(name: string): string {
  return name
    .trim()
    .split(/\s+/u)
    .filter((part) => /\p{L}/u.test(part))
    .slice(0, 2)
    .map((part) => [...part][0] ?? '')
    .join('')
    .toUpperCase();
}

function arabicLetter(name: string): string {
  const parts = name.trim().split(/\s+/u).filter((part) => part !== '');
  const word = parts.find((part) => !KIND_WORDS.has(part)) ?? parts[0] ?? '';
  const bare = word.startsWith('ال') && [...word].length > 2 ? word.slice(2) : word;
  return [...bare.replace(/[()«»"']/gu, '')][0] ?? '';
}

/**
 * «باقة الاحترافية» · "Professional plan", as the detail's frames write it — but a name that already
 * says it is a plan («الباقة الأساسية», "Basic Plan") is written as it is, never «باقة الباقة …».
 */
export function planWords(words: TranslocoService, plan: { nameAr: string; nameEn: string }, language: OpsLanguage): string {
  const name = clinicName(plan, language).trim();
  const named = language === 'ar' ? /^(ال)?باقة(\s|$)/u.test(name) : /\bplan\b/iu.test(name);
  return named ? name : words.translate('app.clinics.detail.plan', { name });
}

/** «تجربة» · "Trial", from the server's words. */
export function statusWord(status: ClinicStatus, language: OpsLanguage): string {
  return language === 'ar' ? status.ar : status.en;
}

/** The badge's intent for a status. */
export function statusIntent(status: ClinicStatus): 'info' | 'success' | 'neutral' {
  return status.tone;
}

/** Whole days from now to a moment, counted up: a trial ending in 3 days 2 hours has 4 days left. */
export function daysUntil(moment: string, now: number): number {
  return Math.max(0, Math.ceil((new Date(moment).getTime() - now) / 86_400_000));
}

/** «خلال 4 أيام» · "in 4 days". */
export function endsIn(words: TranslocoService, moment: string, now: number, language: OpsLanguage): string {
  const days = countWords(words, 'app.clinics.days', daysUntil(moment, now), language);
  return words.translate('app.clinics.list.endsIn', { days });
}

/** «تجربة 14 يومًا», «تجربة 7 أيام» · "14-day trial": Arabic in its count forms (the owner's answer of 17 Oct 2026). */
export function trialLength(words: TranslocoService, days: number, language: OpsLanguage): string {
  const count = language === 'ar' ? countWords(words, 'app.clinics.days', days, language) : opsNumber(days, language);
  return words.translate('app.clinics.list.trialLength', { days: count });
}

/** «0 أطباء · 1 فرع» · "0 doctors · 1 branch". */
export function doctorsAndBranches(words: TranslocoService, doctors: number, branches: number, language: OpsLanguage): string {
  return `${countWords(words, 'app.clinics.doctors', doctors, language)} · ${countWords(words, 'app.clinics.branches', branches, language)}`;
}
