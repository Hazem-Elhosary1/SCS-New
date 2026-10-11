import { ago } from './ago';

// Three in the afternoon in Cairo, on Sunday 11 October 2026.
const NOW = Date.parse('2026-10-11T12:00:00Z');
const before = (seconds: number) => new Date(NOW - seconds * 1000).toISOString();

const MINUTE = 60;
const HOUR = 3600;
const DAY = 86_400;

describe('when a notification came, as the design writes it', () => {
  it.each([
    [10, 'الآن', 'Just now'],
    [MINUTE, 'منذ دقيقة', '1 min ago'],
    [2 * MINUTE, 'منذ دقيقتين', '2 min ago'],
    [5 * MINUTE, 'منذ ٥ دقائق', '5 min ago'],
    [45 * MINUTE, 'منذ ٤٥ دقيقة', '45 min ago'],
    [HOUR, 'منذ ساعة', '1 h ago'],
    [2 * HOUR, 'منذ ساعتين', '2 h ago'],
    [3 * HOUR, 'منذ ٣ ساعات', '3 h ago'],
    [6 * HOUR, 'اليوم، ٩:٠٠ ص', 'Today, 9:00 AM'],
    [14 * HOUR + 30 * MINUTE, 'اليوم، ١٢:٣٠ ص', 'Today, 12:30 AM'],
    [16 * HOUR, 'أمس', 'Yesterday'],
    [38 * HOUR, 'أمس', 'Yesterday'],
    [40 * HOUR, '٩ أكتوبر', '9 Oct'],
    [24 * DAY, '١٧ سبتمبر', '17 Sep'],
    [300 * DAY, '١٥ ديسمبر ٢٠٢٥', '15 Dec 2025'],
  ])('%d seconds before reads «%s» · "%s"', (seconds, arabic, english) => {
    expect(ago(before(seconds), 'ar', NOW)).toBe(arabic);
    expect(ago(before(seconds), 'en', NOW)).toBe(english);
  });

  it('a few hours ago stays "ago" across midnight', () => {
    // Two in the morning in Cairo; three hours before is yesterday's eleven at night.
    const night = Date.parse('2026-10-10T23:00:00Z');

    expect(ago(new Date(night - 3 * HOUR * 1000).toISOString(), 'en', night)).toBe('3 h ago');
  });

  it('a clock a little behind the platform’s does not speak of the future', () => {
    expect(ago(new Date(NOW + 5000).toISOString(), 'ar', NOW)).toBe('الآن');
    expect(ago(new Date(NOW + 5000).toISOString(), 'en', NOW)).toBe('Just now');
  });
});
