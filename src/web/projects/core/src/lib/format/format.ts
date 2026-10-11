import { InjectionToken, Pipe, PipeTransform, inject } from '@angular/core';
import { LanguageService, ScsLanguage } from '../i18n/language.service';

export const CAIRO_TIME_ZONE = 'Africa/Cairo';
export const CURRENCY = 'EGP';

/**
 * Which digits Arabic screens show: `arab` (٠١٢٣) or `latn` (0123). One setting for the whole
 * product; the default follows the design file. To be confirmed in spec 006.
 */
export const SCS_ARABIC_DIGITS = new InjectionToken<'arab' | 'latn'>('SCS_ARABIC_DIGITS', {
  providedIn: 'root',
  factory: () => 'arab',
});

export type ScsDateFormat = 'date' | 'time' | 'dateTime' | 'long';

function localeFor(language: ScsLanguage, arabicDigits: 'arab' | 'latn'): string {
  return language === 'ar' ? `ar-EG-u-nu-${arabicDigits}` : 'en-GB';
}

const DATE_OPTIONS: Record<Exclude<ScsDateFormat, 'long'>, Intl.DateTimeFormatOptions> = {
  date: { dateStyle: 'medium' },
  time: { timeStyle: 'short' },
  dateTime: { dateStyle: 'medium', timeStyle: 'short' },
};

/**
 * The line under a page's title, as the design writes it: «الجمعة ٢١ أغسطس ٢٠٢٦، ٣:٠٤ م» and
 * "Friday 21 August 2026, 3:04 PM" — the day by name, the date in words, a twelve-hour time.
 */
function longDate(instant: Date, language: ScsLanguage, arabicDigits: 'arab' | 'latn'): string {
  const dateLocale = localeFor(language, arabicDigits);
  const timeLocale = language === 'ar' ? dateLocale : 'en-US';

  const parts = new Intl.DateTimeFormat(dateLocale, {
    weekday: 'long',
    day: 'numeric',
    month: 'long',
    year: 'numeric',
    timeZone: CAIRO_TIME_ZONE,
  }).formatToParts(instant);
  const part = (type: Intl.DateTimeFormatPartTypes) =>
    parts.find((found) => found.type === type)?.value ?? '';

  const time = new Intl.DateTimeFormat(timeLocale, {
    hour: 'numeric',
    minute: '2-digit',
    hour12: true,
    timeZone: CAIRO_TIME_ZONE,
  }).format(instant);

  const comma = language === 'ar' ? '،' : ',';
  return `${part('weekday')} ${part('day')} ${part('month')} ${part('year')}${comma} ${time}`;
}

/**
 * Shows an instant in Cairo time, in the active language (SH-001). The platform sends UTC;
 * no component formats a date itself.
 */
@Pipe({ name: 'scsDate', pure: false })
export class ScsDatePipe implements PipeTransform {
  private readonly language = inject(LanguageService).language;
  private readonly digits = inject(SCS_ARABIC_DIGITS);

  transform(value: string | Date | null | undefined, format: ScsDateFormat = 'dateTime'): string {
    if (value === null || value === undefined || value === '') {
      return '';
    }

    const instant = value instanceof Date ? value : new Date(value);
    if (Number.isNaN(instant.getTime())) {
      return '';
    }

    if (format === 'long') {
      return longDate(instant, this.language(), this.digits);
    }

    return new Intl.DateTimeFormat(localeFor(this.language(), this.digits), {
      ...DATE_OPTIONS[format],
      timeZone: CAIRO_TIME_ZONE,
    }).format(instant);
  }
}

/**
 * Shows an amount in Egyptian pounds, in the active language (SH-001). The platform sends
 * money as a decimal string, never as a floating-point number.
 */
@Pipe({ name: 'scsMoney', pure: false })
export class ScsMoneyPipe implements PipeTransform {
  private readonly language = inject(LanguageService).language;
  private readonly digits = inject(SCS_ARABIC_DIGITS);

  transform(value: string | null | undefined): string {
    if (value === null || value === undefined || !/^-?\d+(\.\d+)?$/.test(value)) {
      return '';
    }

    return new Intl.NumberFormat(localeFor(this.language(), this.digits), {
      style: 'currency',
      currency: CURRENCY,
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    }).format(Number(value));
  }
}
