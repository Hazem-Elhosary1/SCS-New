import { ErrorHandler } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  TRANSLOCO_MISSING_HANDLER,
  TranslocoService,
  TranslocoTestingModule,
} from '@jsverse/transloco';
import { MissingTranslationError, ScsMissingHandler } from './i18n.providers';

describe('ScsMissingHandler', () => {
  let transloco: TranslocoService;
  let reported: unknown[];

  beforeEach(() => {
    reported = [];

    TestBed.configureTestingModule({
      imports: [
        TranslocoTestingModule.forRoot({
          langs: {
            ar: { shared: { both: 'نصّ', arabicOnly: 'بالعربيّة فقط' } },
            en: { shared: { both: 'Text', englishOnly: 'English only' } },
          },
          translocoConfig: {
            availableLangs: ['ar', 'en'],
            defaultLang: 'ar',
            missingHandler: { logMissingKey: false, allowEmpty: false },
          },
          preloadLangs: true,
        }),
      ],
      providers: [
        { provide: TRANSLOCO_MISSING_HANDLER, useClass: ScsMissingHandler },
        { provide: ErrorHandler, useValue: { handleError: (e: unknown) => reported.push(e) } },
      ],
    });
    transloco = TestBed.inject(TranslocoService);
  });

  const settle = () => new Promise((resolve) => setTimeout(resolve));

  it('leaves a translated text alone and reports nothing', async () => {
    expect(transloco.translate('shared.both')).toBe('نصّ');

    await settle();
    expect(reported).toEqual([]);
  });

  it('shows the English text when the Arabic one is missing', () => {
    expect(transloco.translate('shared.englishOnly')).toBe('English only');
  });

  it('shows the Arabic text when the English one is missing', () => {
    transloco.setActiveLang('en');

    expect(transloco.translate('shared.arabicOnly')).toBe('بالعربيّة فقط');
  });

  it('shows the key when the text is missing in both languages', () => {
    expect(transloco.translate('shared.nowhere')).toBe('shared.nowhere');
  });

  it('reports a missing text once, naming the key and the language', async () => {
    transloco.translate('shared.englishOnly');
    transloco.translate('shared.englishOnly');
    transloco.translate('shared.englishOnly');
    await settle();

    expect(reported.length).toBe(1);
    expect(reported[0]).toBeInstanceOf(MissingTranslationError);
    expect((reported[0] as Error).message).toContain('shared.englishOnly');
    expect((reported[0] as Error).message).toContain('"ar"');
  });

  it('reports the same key again for the other language', async () => {
    transloco.translate('shared.nowhere');
    transloco.setActiveLang('en');
    transloco.translate('shared.nowhere');
    await settle();

    expect(reported.length).toBe(2);
  });
});
