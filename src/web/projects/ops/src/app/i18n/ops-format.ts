/**
 * How the operators console writes numbers, dates and times (spec 011, FR-049; research R15):
 * always Western digits, with the words of the page's language — «منذ 5 دقائق», «12 يناير
 * 2026», "Yesterday, 4:30 PM". Every number, count, countdown, date and time of the `ops`
 * application goes through here; the clinic's console keeps its own rule.
 */
export type OpsLanguage = 'ar' | 'en';

const LOCALE: Record<OpsLanguage, string> = { ar: 'ar-EG-u-nu-latn', en: 'en-US' };

const DAY = 86_400_000;

/** Up to this many hours back, a time is said as "hours ago"; after that, by its day and clock. */
const RECENT_HOURS = 3;

/** Marks a formatter adds for direction, and the narrow spaces before AM/PM. */
function tidy(text: string): string {
  return text.replace(/[\u200e\u200f\u061c]/g, '').replace(/[\u00a0\u202f]/g, ' ');
}

function toDate(value: string | number | Date): Date {
  return value instanceof Date ? value : new Date(value);
}

/** A whole number or a decimal, grouped as the language groups, in Western digits. */
export function opsNumber(value: number, language: OpsLanguage, options: Intl.NumberFormatOptions = {}): string {
  return tidy(new Intl.NumberFormat(LOCALE[language], options).format(value));
}

/** A share of a whole: `75%`. The sign is the Western one in both languages. */
export function opsPercent(part: number, whole: number, language: OpsLanguage): string {
  const share = whole <= 0 ? 0 : Math.round((part / whole) * 100);
  return `${opsNumber(share, language)}%`;
}

/** Minutes and seconds left: `01:05`. */
export function opsCountdown(totalSeconds: number): string {
  const seconds = Math.max(0, Math.floor(totalSeconds));
  const two = (value: number) => String(value).padStart(2, '0');
  return `${two(Math.floor(seconds / 60))}:${two(seconds % 60)}`;
}

/** «4:30 م» · "4:30 PM". */
export function opsTime(value: string | number | Date, language: OpsLanguage): string {
  return tidy(
    new Intl.DateTimeFormat(LOCALE[language], { hour: 'numeric', minute: '2-digit', hour12: true }).format(toDate(value)),
  );
}

/** «12 يناير 2026» · "January 12, 2026". */
export function opsDate(value: string | number | Date, language: OpsLanguage): string {
  return tidy(
    new Intl.DateTimeFormat(LOCALE[language], { day: 'numeric', month: 'long', year: 'numeric' }).format(toDate(value)),
  );
}

/** «12 يناير 2026 · 4:30 م» · "January 12, 2026, 4:30 PM". */
export function opsDateTime(value: string | number | Date, language: OpsLanguage): string {
  const separator = language === 'ar' ? ' · ' : ', ';
  return `${opsDate(value, language)}${separator}${opsTime(value, language)}`;
}

function daysApart(from: Date, now: Date): number {
  const start = (date: Date) => new Date(date.getFullYear(), date.getMonth(), date.getDate()).getTime();
  return Math.round((start(now) - start(from)) / DAY);
}

function ago(value: number, unit: 'minute' | 'hour' | 'day', language: OpsLanguage): string {
  const text = tidy(new Intl.RelativeTimeFormat(LOCALE[language], { numeric: 'always' }).format(-value, unit));
  // The frames say «منذ», where the browser's own word is «قبل».
  // And «منذ ساعة», not «منذ ساعة واحدة»: one of a thing is the thing.
  return language === 'ar' ? text.replace(/^قبل\s/u, 'منذ ').replace(/\s(?:واحدة|واحد)$/u, '') : text;
}

/** "Two hours ago", whatever the day: «منذ ساعتين» · "2 hours ago". Minutes, then hours, then days. */
export function opsAgo(from: string | number | Date, now: number, language: OpsLanguage): string {
  const seconds = Math.max(0, Math.round((now - toDate(from).getTime()) / 1000));
  if (seconds < 3600) {
    return ago(Math.max(1, Math.round(seconds / 60)), 'minute', language);
  }
  return seconds < 86_400
    ? ago(Math.round(seconds / 3600), 'hour', language)
    : ago(Math.round(seconds / 86_400), 'day', language);
}

/**
 * When something happened, as the frames of the team's list write it: within the hour «منذ 5
 * دقائق»; in the last few hours «منذ ساعتين»; earlier today «اليوم · 9:05 ص»; the day before
 * «أمس · 4:30 م»; within the month «منذ 3 أيام»; before that, the date.
 */
export function opsRelative(from: string | number | Date, now: number, language: OpsLanguage): string {
  const then = toDate(from);
  const seconds = Math.max(0, Math.round((now - then.getTime()) / 1000));
  if (seconds < 3600) {
    return ago(Math.max(1, Math.round(seconds / 60)), 'minute', language);
  }

  if (seconds < RECENT_HOURS * 3600) {
    return ago(Math.floor(seconds / 3600), 'hour', language);
  }

  const days = daysApart(then, new Date(now));
  const separator = language === 'ar' ? ' · ' : ', ';
  if (days <= 0) {
    return `${language === 'ar' ? 'اليوم' : 'Today'}${separator}${opsTime(then, language)}`;
  }
  if (days === 1) {
    return `${language === 'ar' ? 'أمس' : 'Yesterday'}${separator}${opsTime(then, language)}`;
  }
  return days <= 30 ? ago(days, 'day', language) : opsDate(then, language);
}

/** True when a text holds an Arabic-Indic or Persian digit: what no `ops` screen may show. */
export function hasEasternDigit(text: string): boolean {
  return /[\u0660-\u0669\u06f0-\u06f9]/u.test(text);
}

/**
 * The Arabic count of a thing in its grammatical form (contracts/operator-screens.md §7):
 * «مشغل واحد», «مشغلان», «3 مشغلين», «11 مشغلًا», «100 مشغل». English takes one and many.
 */
export interface OpsCountForms {
  readonly zero: string;
  readonly one: string;
  readonly two: string;
  /** 3 to 10. */
  readonly few: string;
  /** 11 to 99. */
  readonly many: string;
  /** Hundreds and thousands, exactly. */
  readonly other: string;
}

export function opsCount(value: number, forms: OpsCountForms, language: OpsLanguage, english: readonly [string, string]): string {
  const number = opsNumber(value, language);
  if (language === 'en') {
    return `${number} ${value === 1 ? english[0] : english[1]}`;
  }
  if (value === 0) {
    return forms.zero;
  }
  if (value === 1) {
    return forms.one;
  }
  if (value === 2) {
    return forms.two;
  }
  const tail = value % 100;
  if (tail >= 3 && tail <= 10) {
    return `${number} ${forms.few}`;
  }
  return tail >= 11 ? `${number} ${forms.many}` : `${number} ${forms.other}`;
}
