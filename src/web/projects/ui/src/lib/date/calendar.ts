/**
 * Days, months and their names without a library (spec 095, research R11).
 *
 * A day is written `YYYY-MM-DD` and means a whole day on the calendar, not an instant: it is
 * never shifted by the reader's time zone. "Today" is today in Cairo (SH-001). The week starts
 * on Sunday in the operators' calendar (`75:4`; the Arabic one, `1923:9835`, mirrors it with Sunday on
 * the right) and on Saturday in the clinic's (`4256:887`). Names follow the page's language; digits
 * follow the console's rule — Western in the operators console (OP-B-386), Arabic-Indic in the clinic.
 */

/** A whole day, `YYYY-MM-DD`. */
export type ScsDay = string;

/** The page's language. */
export type ScsDateLanguage = 'ar' | 'en';

/** The digits of an Arabic page: Arabic-Indic `٠–٩` (`arab`) or Western `0–9` (`latn`), as core's `SCS_ARABIC_DIGITS`. */
export type ScsDigits = 'arab' | 'latn';

/** A start and an end of whole days, the end on or after the start; an open end is `null`. */
export interface ScsDayRange {
  readonly start: ScsDay;
  readonly end: ScsDay | null;
}

/** One cell of a month's grid. */
export interface ScsMonthCell {
  readonly day: ScsDay;
  /** The day of the month, 1–31. */
  readonly date: number;
  /** The cell belongs to the month shown (the others are the edges of the months beside it). */
  readonly inMonth: boolean;
}

/** The week's first day as the operators' calendar draws it (`75:4`): Sunday. 0 is Sunday, 6 Saturday. */
export const SCS_WEEK_START = 0;

/** The clinic's calendar (`4256:887`) starts its week on Saturday. */
export const SCS_WEEK_START_SATURDAY = 6;

/** Cairo, where every clinic and the operators work (SH-001). */
export const SCS_TIME_ZONE = 'Africa/Cairo';

const DAY = /^(\d{4})-(\d{2})-(\d{2})$/;

/** The day written `YYYY-MM-DD` from its parts; `month` is 1–12. */
export function dayOf(year: number, month: number, date: number): ScsDay {
  return `${String(year).padStart(4, '0')}-${String(month).padStart(2, '0')}-${String(date).padStart(2, '0')}`;
}

/** The parts of a day, or `null` when it is not a real day (`2026-02-30`, `2026-13-01`, `soon`). */
export function partsOf(day: ScsDay): { year: number; month: number; date: number } | null {
  const match = DAY.exec(day);
  if (!match) return null;
  const [year, month, date] = [Number(match[1]), Number(match[2]), Number(match[3])];
  if (month < 1 || month > 12 || date < 1 || date > daysInMonth(year, month)) return null;
  return { year, month, date };
}

/** A real day. */
export function isDay(day: unknown): day is ScsDay {
  return typeof day === 'string' && partsOf(day) !== null;
}

/** The year is a leap year (every fourth, but not centuries unless the fourth century). */
export function isLeapYear(year: number): boolean {
  return (year % 4 === 0 && year % 100 !== 0) || year % 400 === 0;
}

/** The days in a month; `month` is 1–12. */
export function daysInMonth(year: number, month: number): number {
  return [31, isLeapYear(year) ? 29 : 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31][month - 1];
}

/** The day as a moment at noon UTC, so no time zone moves it to the day before or after. */
function noon(day: ScsDay): Date {
  const parts = partsOf(day);
  if (!parts) throw new RangeError(`Not a day: ${day}`);
  return new Date(Date.UTC(parts.year, parts.month - 1, parts.date, 12));
}

function fromNoon(moment: Date): ScsDay {
  return dayOf(moment.getUTCFullYear(), moment.getUTCMonth() + 1, moment.getUTCDate());
}

/** The day of the week, 0 Sunday … 6 Saturday. */
export function weekdayOf(day: ScsDay): number {
  return noon(day).getUTCDay();
}

/** The day `count` days later (or earlier, when negative). */
export function addDays(day: ScsDay, count: number): ScsDay {
  const moment = noon(day);
  moment.setUTCDate(moment.getUTCDate() + count);
  return fromNoon(moment);
}

/** The same day `count` months later; a day the month lacks becomes its last (31 Jan + 1 → 28 or 29 Feb). */
export function addMonths(day: ScsDay, count: number): ScsDay {
  const parts = partsOf(day);
  if (!parts) throw new RangeError(`Not a day: ${day}`);
  const index = parts.year * 12 + (parts.month - 1) + count;
  const year = Math.floor(index / 12);
  const month = (index % 12) + 1;
  return dayOf(year, month, Math.min(parts.date, daysInMonth(year, month)));
}

/** The first and last day of the week the day is in. */
export function weekEdges(day: ScsDay, weekStart = SCS_WEEK_START): { first: ScsDay; last: ScsDay } {
  const back = (weekdayOf(day) - weekStart + 7) % 7;
  const first = addDays(day, -back);
  return { first, last: addDays(first, 6) };
}

/** Days compare as written: earlier is smaller. */
export function compareDays(a: ScsDay, b: ScsDay): number {
  return a < b ? -1 : a > b ? 1 : 0;
}

/** The day lies between `min` and `max`, either of which may be absent. */
export function isWithin(day: ScsDay, min?: ScsDay | null, max?: ScsDay | null): boolean {
  return (!min || compareDays(day, min) >= 0) && (!max || compareDays(day, max) <= 0);
}

/** The day lies inside the range, its ends included. */
export function isInRange(day: ScsDay, range: ScsDayRange | null): boolean {
  return !!range && compareDays(day, range.start) >= 0 && compareDays(day, range.end ?? range.start) <= 0;
}

/** Today in Cairo, whatever the reader's clock says about their own zone. */
export function todayInCairo(now: Date = new Date()): ScsDay {
  // en-CA writes YYYY-MM-DD.
  return new Intl.DateTimeFormat('en-CA', { timeZone: SCS_TIME_ZONE, year: 'numeric', month: '2-digit', day: '2-digit' }).format(now);
}

/**
 * The month's grid: six weeks of seven days, from the week holding the first, so the panel keeps
 * its height from month to month (`75:4` draws six rows, the edges of July and September in grey).
 */
export function monthGrid(year: number, month: number, weekStart = SCS_WEEK_START): ScsMonthCell[][] {
  const first = weekEdges(dayOf(year, month, 1), weekStart).first;
  return Array.from({ length: 6 }, (_, week) =>
    Array.from({ length: 7 }, (_, column) => {
      const day = addDays(first, week * 7 + column);
      const parts = partsOf(day)!;
      return { day, date: parts.date, inMonth: parts.month === month && parts.year === year };
    }),
  );
}

function locale(language: ScsDateLanguage, digits: ScsDigits): string {
  if (language === 'en') return 'en-US';
  return `ar-EG-u-nu-${digits}`;
}

function format(day: ScsDay, language: ScsDateLanguage, digits: ScsDigits, options: Intl.DateTimeFormatOptions): string {
  return new Intl.DateTimeFormat(locale(language, digits), { timeZone: 'UTC', ...options }).format(noon(day));
}

/** The panel's title: "August 2026" · «أغسطس 2026». */
export function monthTitle(year: number, month: number, language: ScsDateLanguage, digits: ScsDigits = 'latn'): string {
  return format(dayOf(year, month, 1), language, digits, { month: 'long', year: 'numeric' });
}

/**
 * The column heads from the week's first day: "Su Mo Tu …" in English, «ح ن ث …» in Arabic (as drawn),
 * each with the day's whole name for the reader of the screen.
 */
export function weekdayNames(language: ScsDateLanguage, weekStart = SCS_WEEK_START): { short: string; long: string }[] {
  // 2 August 2026 is a Sunday.
  return Array.from({ length: 7 }, (_, index) => {
    const day = dayOf(2026, 8, 2 + ((weekStart + index) % 7));
    const short =
      language === 'en' ? format(day, 'en', 'latn', { weekday: 'short' }).slice(0, 2) : format(day, 'ar', 'latn', { weekday: 'narrow' });
    return { short, long: format(day, language, 'latn', { weekday: 'long' }) };
  });
}

/** The day as the field writes it: "Aug 28, 2026" · «28 أغسطس 2026». */
export function formatDay(day: ScsDay, language: ScsDateLanguage, digits: ScsDigits = 'latn'): string {
  return format(day, language, digits, language === 'en' ? { day: 'numeric', month: 'short', year: 'numeric' } : { day: 'numeric', month: 'long', year: 'numeric' });
}

/** The day in full, for the reader of the screen: "Friday, August 28, 2026" · «الجمعة، 28 أغسطس 2026». */
export function describeDay(day: ScsDay, language: ScsDateLanguage, digits: ScsDigits = 'latn'): string {
  return format(day, language, digits, { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' });
}

/** A number of the grid (a day of the month) in the console's digits. */
export function formatNumber(value: number, language: ScsDateLanguage, digits: ScsDigits = 'latn'): string {
  return new Intl.NumberFormat(locale(language, digits), { useGrouping: false }).format(value);
}

/**
 * A range as the field writes it: "Aug 1 – Aug 28, 2026" · «1 أغسطس – 28 أغسطس 2026» — the year once
 * when both ends share it; an open end leaves the dash.
 */
export function formatRange(range: ScsDayRange, language: ScsDateLanguage, digits: ScsDigits = 'latn'): string {
  const end = range.end;
  if (!end) return `${formatDay(range.start, language, digits)} –`;
  const sameYear = range.start.slice(0, 4) === end.slice(0, 4);
  const start = sameYear
    ? format(range.start, language, digits, language === 'en' ? { day: 'numeric', month: 'short' } : { day: 'numeric', month: 'long' })
    : formatDay(range.start, language, digits);
  return `${start} – ${formatDay(end, language, digits)}`;
}
