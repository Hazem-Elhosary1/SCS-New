import {
  hasEasternDigit,
  opsAgo,
  opsCount,
  opsCountdown,
  opsDate,
  opsDateTime,
  opsNumber,
  opsPercent,
  opsRelative,
  opsTime, opsCairoDate, opsCairoDateTime } from './ops-format';

/** Spec 011, FR-049: Western digits in both languages, with the language's own words. */
describe('ops-format', () => {
  const now = new Date(2026, 9, 13, 15, 0, 0).getTime();
  const at = (day: number, hour: number, minute = 0) => new Date(2026, 9, day, hour, minute, 0);

  it('writes numbers in Western digits in Arabic and English', () => {
    expect(opsNumber(1234567, 'ar')).toBe('1,234,567');
    expect(opsNumber(1234567, 'en')).toBe('1,234,567');
    expect(opsPercent(3, 4, 'ar')).toBe('75%');
    expect(opsPercent(0, 0, 'en')).toBe('0%');
    expect(opsCountdown(65)).toBe('01:05');
    expect(opsCountdown(-3)).toBe('00:00');
  });

  it('writes a date and a time with the language’s words', () => {
    const when = new Date(2026, 0, 12, 16, 30);
    expect(opsDate(when, 'ar')).toBe('12 يناير 2026');
    expect(opsDate(when, 'en')).toBe('January 12, 2026');
    expect(opsTime(when, 'ar')).toBe('4:30 م');
    expect(opsTime(when, 'en')).toBe('4:30 PM');
    expect(opsDateTime(when, 'ar')).toBe('12 يناير 2026 · 4:30 م');
    expect(opsDateTime(when, 'en')).toBe('January 12, 2026, 4:30 PM');
  });

  it('says when something happened as the frames do', () => {
    expect(opsRelative(at(13, 14, 55), now, 'ar')).toBe('منذ 5 دقائق');
    expect(opsRelative(at(13, 14, 55), now, 'en')).toBe('5 minutes ago');
    expect(opsRelative(at(13, 13, 55), now, 'ar')).toBe('منذ ساعة');
    expect(opsRelative(at(13, 12, 30), now, 'ar')).toBe('منذ ساعتين');
    expect(opsRelative(at(13, 12, 30), now, 'en')).toBe('2 hours ago');
    expect(opsRelative(at(13, 9, 5), now, 'ar')).toBe('اليوم · 9:05 ص');
    expect(opsRelative(at(13, 9, 5), now, 'en')).toBe('Today, 9:05 AM');
    expect(opsRelative(at(12, 16, 30), now, 'ar')).toBe('أمس · 4:30 م');
    expect(opsRelative(at(12, 16, 30), now, 'en')).toBe('Yesterday, 4:30 PM');
    expect(opsRelative(at(10, 16, 30), now, 'ar')).toBe('منذ 3 أيام');
    expect(opsRelative(at(10, 16, 30), now, 'en')).toBe('3 days ago');
    expect(opsRelative(new Date(2026, 0, 12, 16, 30), now, 'ar')).toBe('12 يناير 2026');
    expect(opsRelative(new Date(2026, 0, 12, 16, 30), now, 'en')).toBe('January 12, 2026');
  });

  it('never says less than a minute, and counts hours and days for a session', () => {
    expect(opsRelative(now - 5_000, now, 'en')).toBe('1 minute ago');
    expect(opsAgo(now - 2 * 3600_000, now, 'ar')).toBe('منذ ساعتين');
    expect(opsAgo(now - 2 * 3600_000, now, 'en')).toBe('2 hours ago');
    expect(opsAgo(now - 3 * 86_400_000, now, 'ar')).toBe('منذ 3 أيام');
  });

  it('counts in Arabic in its grammatical form', () => {
    const forms = { zero: 'لا مشغلين', one: 'مشغل واحد', two: 'مشغلان', few: 'مشغلين', many: 'مشغلًا', other: 'مشغل' };
    const say = (value: number) => opsCount(value, forms, 'ar', ['operator', 'operators']);
    expect([0, 1, 2, 3, 10, 11, 99, 100, 103].map(say)).toEqual([
      'لا مشغلين', 'مشغل واحد', 'مشغلان', '3 مشغلين', '10 مشغلين', '11 مشغلًا', '99 مشغلًا', '100 مشغل', '103 مشغلين',
    ]);
    expect(opsCount(1, forms, 'en', ['operator', 'operators'])).toBe('1 operator');
    expect(opsCount(12, forms, 'en', ['operator', 'operators'])).toBe('12 operators');
  });

  it('lets no Arabic-Indic digit out, whatever is asked', () => {
    const all = [
      opsNumber(9876543210, 'ar'),
      opsPercent(1, 3, 'ar'),
      opsDate(now, 'ar'),
      opsTime(now, 'ar'),
      opsDateTime(now, 'ar'),
      opsRelative(now - 40 * 60_000, now, 'ar'),
      opsRelative(now - 9 * 86_400_000, now, 'ar'),
      opsRelative(now - 400 * 86_400_000, now, 'ar'),
      opsAgo(now - 7 * 3600_000, now, 'ar'),
    ];
    for (const text of all) {
      expect(hasEasternDigit(text), text).toBe(false);
    }
    expect(hasEasternDigit('١٢')).toBe(true);
    expect(hasEasternDigit('۴')).toBe(true);
  });

  it('writes a clinic’s dates in Cairo, whatever the browser’s zone (spec 015)', () => {
    // 21:59 and 22:00 UTC on 31 October 2026: Cairo (UTC+2) is still the 31st, then the 1st of November.
    expect(opsCairoDate('2026-10-31T21:59:00Z', 'en')).toBe('October 31, 2026');
    expect(opsCairoDate('2026-10-31T22:00:00Z', 'en')).toBe('November 1, 2026');
    expect(opsCairoDate('2026-10-31T22:00:00Z', 'ar')).toBe('1 نوفمبر 2026');
    expect(opsCairoDateTime('2026-10-31T21:59:00Z', 'en')).toBe('October 31, 2026 · 11:59 PM');
    expect(hasEasternDigit(opsCairoDateTime('2026-10-31T21:59:00Z', 'ar'))).toBe(false);
  });
});
