import {
  addDays,
  formatRange,
  addMonths,
  daysInMonth,
  describeDay,
  formatDay,
  formatNumber,
  isDay,
  isInRange,
  isLeapYear,
  isWithin,
  monthGrid,
  monthTitle,
  todayInCairo,
  weekdayNames,
  weekEdges,
} from './calendar';

/** The dates under the calendar (spec 095, research R11). */
describe('calendar days', () => {
  it('draws August 2026 as the panel does: six weeks from Sunday 26 July to Saturday 5 September', () => {
    const grid = monthGrid(2026, 8);
    expect(grid.length).toBe(6);
    expect(grid.every((week) => week.length === 7)).toBe(true);
    expect(grid[0][0]).toEqual({ day: '2026-07-26', date: 26, inMonth: false });
    expect(grid[0][6]).toEqual({ day: '2026-08-01', date: 1, inMonth: true });
    expect(grid[5][6]).toEqual({ day: '2026-09-05', date: 5, inMonth: false });
    expect(grid.flat().filter((cell) => cell.inMonth).length).toBe(31);
  });

  it('starts a month that begins on Sunday in its first cell', () => {
    // 1 February 2026 is a Sunday.
    expect(monthGrid(2026, 2)[0][0]).toEqual({ day: '2026-02-01', date: 1, inMonth: true });
  });

  it('knows leap years and the length of each month', () => {
    expect([2024, 2026, 1900, 2000].map(isLeapYear)).toEqual([true, false, false, true]);
    expect(daysInMonth(2024, 2)).toBe(29);
    expect(daysInMonth(2026, 2)).toBe(28);
    expect(daysInMonth(2026, 4)).toBe(30);
    expect(daysInMonth(2026, 12)).toBe(31);
    expect(monthGrid(2024, 2).flat().filter((cell) => cell.inMonth).length).toBe(29);
  });

  it('refuses days that are not', () => {
    expect(isDay('2026-08-28')).toBe(true);
    expect(isDay('2026-02-29')).toBe(false);
    expect(isDay('2024-02-29')).toBe(true);
    expect(isDay('2026-13-01')).toBe(false);
    expect(isDay('28/08/2026')).toBe(false);
  });

  it('moves by days across months and years, and by months to the last day a month has', () => {
    expect(addDays('2026-08-31', 1)).toBe('2026-09-01');
    expect(addDays('2026-01-01', -1)).toBe('2025-12-31');
    expect(addDays('2024-02-28', 1)).toBe('2024-02-29');
    expect(addMonths('2026-01-31', 1)).toBe('2026-02-28');
    expect(addMonths('2024-01-31', 1)).toBe('2024-02-29');
    expect(addMonths('2026-12-15', 1)).toBe('2027-01-15');
    expect(addMonths('2026-01-15', -1)).toBe('2025-12-15');
  });

  it('finds the edges of a week from Sunday to Saturday', () => {
    expect(weekEdges('2026-08-28')).toEqual({ first: '2026-08-23', last: '2026-08-29' });
    expect(weekEdges('2026-08-23')).toEqual({ first: '2026-08-23', last: '2026-08-29' });
  });

  it('tells whether a day is allowed and whether it is in a range', () => {
    expect(isWithin('2026-08-05', '2026-08-07')).toBe(false);
    expect(isWithin('2026-08-07', '2026-08-07', '2026-08-31')).toBe(true);
    expect(isInRange('2026-08-12', { start: '2026-08-10', end: '2026-08-18' })).toBe(true);
    expect(isInRange('2026-08-18', { start: '2026-08-10', end: '2026-08-18' })).toBe(true);
    expect(isInRange('2026-08-19', { start: '2026-08-10', end: '2026-08-18' })).toBe(false);
    expect(isInRange('2026-08-10', { start: '2026-08-10', end: null })).toBe(true);
  });

  it('counts today in Cairo: past its midnight it is already tomorrow, wherever the reader is', () => {
    // 21:59 UTC on 27 August is 00:59 in Cairo (summer time, UTC+3) on the 28th.
    expect(todayInCairo(new Date(Date.UTC(2026, 7, 27, 21, 59)))).toBe('2026-08-28');
    expect(todayInCairo(new Date(Date.UTC(2026, 7, 27, 20, 59)))).toBe('2026-08-27');
    // In winter Cairo is UTC+2: 22:30 UTC on 31 December is 00:30 on 1 January.
    expect(todayInCairo(new Date(Date.UTC(2026, 11, 31, 22, 30)))).toBe('2027-01-01');
  });

  it('names months and days in the page language, with Western digits in the operators console', () => {
    expect(monthTitle(2026, 8, 'en')).toBe('August 2026');
    expect(monthTitle(2026, 8, 'ar')).toBe('أغسطس 2026');
    expect(weekdayNames('en').map((name) => name.short)).toEqual(['Su', 'Mo', 'Tu', 'We', 'Th', 'Fr', 'Sa']);
    expect(weekdayNames('ar').map((name) => name.short)).toEqual(['ح', 'ن', 'ث', 'ر', 'خ', 'ج', 'س']);
    expect(weekdayNames('ar')[0].long).toBe('الأحد');
    expect(formatDay('2026-08-28', 'en')).toBe('Aug 28, 2026');
    expect(formatDay('2026-08-28', 'ar')).toBe('28 أغسطس 2026');
    expect(describeDay('2026-08-28', 'en')).toBe('Friday, August 28, 2026');
    expect(formatNumber(28, 'ar')).toBe('28');
  });

  it('writes Arabic-Indic digits where the console asks for them (the clinic)', () => {
    expect(formatDay('2026-08-28', 'ar', 'arab')).toBe('٢٨ أغسطس ٢٠٢٦');
    expect(formatNumber(28, 'ar', 'arab')).toBe('٢٨');
    expect(formatNumber(28, 'en', 'arab')).toBe('28');
  });

  it('writes a range with its year once', () => {
    expect(formatRange({ start: '2026-08-01', end: '2026-08-28' }, 'en')).toBe('Aug 1 – Aug 28, 2026');
    expect(formatRange({ start: '2026-08-01', end: '2026-08-28' }, 'ar')).toBe('1 أغسطس – 28 أغسطس 2026');
    expect(formatRange({ start: '2025-12-30', end: '2026-01-02' }, 'en')).toBe('Dec 30, 2025 – Jan 2, 2026');
    expect(formatRange({ start: '2026-08-01', end: null }, 'en')).toBe('Aug 1, 2026 –');
  });
});
