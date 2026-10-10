import { TranslocoTestingModule } from '@jsverse/transloco';
import ar from '../../i18n/ar.json';
import en from '../../i18n/en.json';

/** The shared library's real texts, for unit tests. `app` texts stand in for an app's file. */
export function uiTranslations(active: 'ar' | 'en' = 'ar') {
  return TranslocoTestingModule.forRoot({
    langs: {
      ar: { ...ar, app: { shell: { name: 'لوحة العيادة' } } },
      en: { ...en, app: { shell: { name: 'Clinic console' } } },
    },
    translocoConfig: {
      availableLangs: ['ar', 'en'],
      defaultLang: active,
      reRenderOnLangChange: true,
    },
    preloadLangs: true,
  });
}
