import { CAIRO_TIME_ZONE } from 'core';

const MINUTE = 60;
const HOUR = 60 * MINUTE;
const DAY = 24 * HOUR;

/** Up to here a time today reads as how long ago; after it, as the hour it came. */
const HOURS_AGO_UNTIL = 6 * HOUR;

const arabicDigits = new Intl.NumberFormat('ar-EG-u-nu-arab', { useGrouping: false });

/** One, two, a few, many — as Arabic counts them. */
function arabic(count: number, one: string, two: string, few: string, many: string): string {
  if (count === 1) {
    return `منذ ${one}`;
  }
  if (count === 2) {
    return `منذ ${two}`;
  }
  return `منذ ${arabicDigits.format(count)} ${count <= 10 ? few : many}`;
}

/** The day in Cairo an instant falls on, as `2026-10-11`. */
function cairoDay(instant: number): string {
  return new Intl.DateTimeFormat('en-CA', {
    timeZone: CAIRO_TIME_ZONE,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).format(instant);
}

function parts(instant: number, english: boolean, options: Intl.DateTimeFormatOptions) {
  const found = new Intl.DateTimeFormat(english ? 'en-US' : 'ar-EG-u-nu-arab', {
    ...options,
    timeZone: CAIRO_TIME_ZONE,
  }).formatToParts(instant);
  return (type: Intl.DateTimeFormatPartTypes) =>
    found.find((part) => part.type === type)?.value ?? '';
}

/**
 * When a notification came, in the words of the design (component "Notification Panel"):
 * «منذ ساعتين» · "2 h ago", «اليوم، ٩:٠٠ ص» · "Today, 9:00 AM", «أمس» · "Yesterday",
 * «١٧ سبتمبر» · "17 Sep". Days are Cairo's, like every date of the console.
 */
export function ago(at: string, language: 'ar' | 'en', now: number = Date.now()): string {
  const then = Date.parse(at);
  const seconds = Math.max(0, (now - then) / 1000);
  const english = language === 'en';

  if (seconds < MINUTE) {
    return english ? 'Just now' : 'الآن';
  }
  if (seconds < HOUR) {
    const minutes = Math.trunc(seconds / MINUTE);
    return english ? `${minutes} min ago` : arabic(minutes, 'دقيقة', 'دقيقتين', 'دقائق', 'دقيقة');
  }
  if (seconds < HOURS_AGO_UNTIL) {
    const hours = Math.trunc(seconds / HOUR);
    return english ? `${hours} h ago` : arabic(hours, 'ساعة', 'ساعتين', 'ساعات', 'ساعة');
  }

  const day = cairoDay(then);
  if (day === cairoDay(now)) {
    const time = parts(then, english, { hour: 'numeric', minute: '2-digit', hour12: true });
    const clock = `${time('hour')}:${time('minute')} ${time('dayPeriod')}`;
    return english ? `Today, ${clock}` : `اليوم، ${clock}`;
  }
  if (day === cairoDay(now - DAY * 1000)) {
    return english ? 'Yesterday' : 'أمس';
  }

  const sameYear = day.slice(0, 4) === cairoDay(now).slice(0, 4);
  const date = parts(then, english, {
    day: 'numeric',
    month: english ? 'short' : 'long',
    year: 'numeric',
  });
  return `${date('day')} ${date('month')}${sameYear ? '' : ` ${date('year')}`}`;
}
