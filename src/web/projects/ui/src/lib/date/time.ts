import { ScsDateLanguage, ScsDigits } from './calendar';

/**
 * Times of day without a library (spec 095, research R11). A time is written `HH:mm` on the
 * twenty-four-hour clock and is a time on the clinic's wall clock (Cairo), not an instant; it is
 * shown on the twelve-hour clock the pages draw: "2:30 PM" · «2:30 م».
 */

/** A time of day, `HH:mm`. */
export type ScsTime = string;

const TIME = /^([01]\d|2[0-3]):([0-5]\d)$/;

/** A real time of day. */
export function isTime(time: unknown): time is ScsTime {
  return typeof time === 'string' && TIME.test(time);
}

/** Minutes since midnight. */
export function minutesOf(time: ScsTime): number {
  const match = TIME.exec(time);
  if (!match) throw new RangeError(`Not a time: ${time}`);
  return Number(match[1]) * 60 + Number(match[2]);
}

/** The time so many minutes after midnight, round the clock. */
export function timeOf(minutes: number): ScsTime {
  const within = ((minutes % 1440) + 1440) % 1440;
  return `${String(Math.floor(within / 60)).padStart(2, '0')}:${String(within % 60).padStart(2, '0')}`;
}

/** Every `step` minutes from `from` to `to`, both included when they fall on a step. */
export function timesBetween(step: number, from: ScsTime = '00:00', to: ScsTime = '23:59'): ScsTime[] {
  const times: ScsTime[] = [];
  for (let at = minutesOf(from); at <= minutesOf(to); at += Math.max(1, step)) times.push(timeOf(at));
  return times;
}

/** The twelve-hour clock's parts: hour 1–12, minute, and after noon. */
export function clockOf(time: ScsTime): { hour: number; minute: number; pm: boolean } {
  const minutes = minutesOf(time);
  const hour = Math.floor(minutes / 60);
  return { hour: hour % 12 === 0 ? 12 : hour % 12, minute: minutes % 60, pm: hour >= 12 };
}

/** The time from the twelve-hour clock's parts. */
export function fromClock(hour: number, minute: number, pm: boolean): ScsTime {
  return timeOf(((hour % 12) + (pm ? 12 : 0)) * 60 + minute);
}

function locale(language: ScsDateLanguage, digits: ScsDigits): string {
  return language === 'en' ? 'en-US' : `ar-EG-u-nu-${digits}`;
}

function moment(time: ScsTime): Date {
  const minutes = minutesOf(time);
  return new Date(Date.UTC(2026, 0, 1, Math.floor(minutes / 60), minutes % 60));
}

/** The time as the pages write it: "2:30 PM" · «2:30 م». */
export function formatTime(time: ScsTime, language: ScsDateLanguage, digits: ScsDigits = 'latn'): string {
  return new Intl.DateTimeFormat(locale(language, digits), { timeZone: 'UTC', hour: 'numeric', minute: '2-digit', hour12: true }).format(
    moment(time),
  );
}

/** The two halves of the day's names: "AM", "PM" · «ص», «م». */
export function periodNames(language: ScsDateLanguage): [string, string] {
  const period = (time: ScsTime) =>
    new Intl.DateTimeFormat(locale(language, 'latn'), { timeZone: 'UTC', hour: 'numeric', hour12: true })
      .formatToParts(moment(time))
      .find((part) => part.type === 'dayPeriod')?.value ?? '';
  return [period('09:00'), period('15:00')];
}

/** A number of the wheel, two digits: "05" · «٠٥». */
export function twoDigits(value: number, language: ScsDateLanguage, digits: ScsDigits = 'latn'): string {
  return new Intl.NumberFormat(locale(language, digits), { minimumIntegerDigits: 2, useGrouping: false }).format(value);
}
