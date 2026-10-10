import { TestBed } from '@angular/core/testing';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { LanguageService } from '../i18n/language.service';
import { SCS_ARABIC_DIGITS, ScsDatePipe, ScsMoneyPipe } from './format';

describe('formatting', () => {
  function setup(language: 'ar' | 'en', digits?: 'arab' | 'latn') {
    TestBed.configureTestingModule({
      imports: [
        TranslocoTestingModule.forRoot({
          langs: { ar: {}, en: {} },
          translocoConfig: { availableLangs: ['ar', 'en'], defaultLang: 'ar' },
        }),
      ],
      providers: [
        ScsDatePipe,
        ScsMoneyPipe,
        ...(digits ? [{ provide: SCS_ARABIC_DIGITS, useValue: digits }] : []),
      ],
    });
    TestBed.inject(LanguageService).use(language);
    return { date: TestBed.inject(ScsDatePipe), money: TestBed.inject(ScsMoneyPipe) };
  }

  describe('scsDate', () => {
    it('shows Cairo time in summer, when Egypt is three hours ahead of UTC', () => {
      const { date } = setup('en');

      expect(date.transform('2026-07-01T10:00:00Z', 'time')).toBe('13:00');
    });

    it('shows Cairo time in winter, when Egypt is two hours ahead of UTC', () => {
      const { date } = setup('en');

      expect(date.transform('2026-01-15T10:00:00Z', 'time')).toBe('12:00');
    });

    it('moves the date when Cairo is already on the next day', () => {
      const { date } = setup('en');

      expect(date.transform('2026-01-15T23:30:00Z', 'date')).toBe('16 Jan 2026');
    });

    it('offers date, time and both together', () => {
      const { date } = setup('en');
      const instant = '2026-07-01T10:00:00Z';

      expect(date.transform(instant, 'date')).toBe('1 Jul 2026');
      expect(date.transform(instant, 'time')).toBe('13:00');
      expect(date.transform(instant)).toContain('1 Jul 2026');
      expect(date.transform(instant)).toContain('13:00');
    });

    it('differs between Arabic and English', () => {
      const arabic = setup('ar').date.transform('2026-07-01T10:00:00Z');
      TestBed.resetTestingModule();
      const english = setup('en').date.transform('2026-07-01T10:00:00Z');

      expect(arabic).not.toBe(english);
      expect(arabic).toContain('٢٠٢٦');
      expect(arabic).toContain('١:٠٠');
      expect(english).toContain('2026');
    });

    it('accepts a Date and returns nothing for an empty or invalid value', () => {
      const { date } = setup('en');

      expect(date.transform(new Date('2026-07-01T10:00:00Z'), 'time')).toBe('13:00');
      expect(date.transform(null)).toBe('');
      expect(date.transform('')).toBe('');
      expect(date.transform('not a date')).toBe('');
    });
  });

  describe('scsMoney', () => {
    it('shows Egyptian pounds in English', () => {
      const { money } = setup('en');

      expect(money.transform('1250.5')).toContain('1,250.50');
      expect(money.transform('1250.5')).toContain('EGP');
    });

    it('shows Egyptian pounds in Arabic with Arabic-Indic digits by default', () => {
      const { money } = setup('ar');

      expect(money.transform('1250.5')).toContain('١٬٢٥٠٫٥٠');
      expect(money.transform('1250.5')).toContain('ج.م');
    });

    it('follows the one digits setting', () => {
      const { money, date } = setup('ar', 'latn');

      expect(money.transform('1250.5')).toContain('1,250.50');
      expect(date.transform('2026-07-01T10:00:00Z', 'date')).toContain('2026');
    });

    it('handles zero and negative amounts', () => {
      const { money } = setup('en');

      expect(money.transform('0')).toContain('0.00');
      expect(money.transform('-75.25')).toContain('75.25');
      expect(money.transform('-75.25')).toMatch(/-|−/);
    });

    it('returns nothing for a value that is not a decimal string', () => {
      const { money } = setup('en');

      expect(money.transform(null)).toBe('');
      expect(money.transform('')).toBe('');
      expect(money.transform('12,5')).toBe('');
      expect(money.transform('abc')).toBe('');
    });
  });
});
