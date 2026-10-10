import { HttpClient } from '@angular/common/http';
import {
  EnvironmentProviders,
  ErrorHandler,
  Injectable,
  Injector,
  inject,
  isDevMode,
  makeEnvironmentProviders,
  provideAppInitializer,
} from '@angular/core';
import {
  TRANSLOCO_MISSING_HANDLER,
  Translation,
  TranslocoConfig,
  TranslocoLoader,
  TranslocoMissingHandler,
  TranslocoService,
  provideTransloco,
} from '@jsverse/transloco';
import { Observable, catchError, forkJoin, map, of } from 'rxjs';
import {
  DEFAULT_LANGUAGE,
  LanguageService,
  SCS_LANGUAGES,
  rememberedLanguage,
} from './language.service';

/**
 * Loads an app's own texts (`/i18n/<lang>.json`) and the shared library's
 * (`/i18n/ui/<lang>.json`) and joins them. Paths are from the site root, so they work on any
 * path of the patient site.
 */
@Injectable({ providedIn: 'root' })
export class ScsTranslocoLoader implements TranslocoLoader {
  private readonly http = inject(HttpClient);

  getTranslation(language: string): Observable<Translation> {
    const file = (url: string) =>
      this.http.get<Translation>(url).pipe(catchError(() => of<Translation>({})));

    return forkJoin([file(`/i18n/${language}.json`), file(`/i18n/ui/${language}.json`)]).pipe(
      map(([app, ui]) => ({ ...ui, ...app })),
    );
  }
}

export class MissingTranslationError extends Error {
  constructor(key: string, language: string) {
    super(`Missing translation "${key}" in "${language}"`);
    this.name = 'MissingTranslationError';
  }
}

/**
 * A text missing in the active language is shown in the other language rather than as a blank
 * or a key, and is reported once (spec 001, edge cases).
 */
@Injectable()
export class ScsMissingHandler implements TranslocoMissingHandler {
  private readonly injector = inject(Injector);
  private readonly reported = new Set<string>();

  handle(key: string, config: TranslocoConfig): string {
    const transloco = this.injector.get(TranslocoService);
    const active = transloco.getActiveLang();
    const other = active === 'ar' ? 'en' : 'ar';
    const fallback = transloco.getTranslation(other)[key];

    // Before the texts have loaded there is nothing to compare against, so nothing to report.
    const loaded = Object.keys(transloco.getTranslation(active)).length > 0;
    if (loaded && !this.reported.has(`${active}:${key}`)) {
      this.reported.add(`${active}:${key}`);
      queueMicrotask(() =>
        this.injector.get(ErrorHandler).handleError(new MissingTranslationError(key, active)),
      );
    }

    return typeof fallback === 'string' && fallback.length > 0
      ? fallback
      : config.missingHandler?.allowEmpty
        ? ''
        : key;
  }
}

/** Arabic and English, switchable without reload, Arabic by default (SH-002). */
export function provideScsI18n(): EnvironmentProviders {
  return makeEnvironmentProviders([
    provideTransloco({
      config: {
        availableLangs: [...SCS_LANGUAGES],
        defaultLang: rememberedLanguage() ?? DEFAULT_LANGUAGE,
        reRenderOnLangChange: true,
        prodMode: !isDevMode(),
        missingHandler: { logMissingKey: false, allowEmpty: false },
      },
      loader: ScsTranslocoLoader,
    }),
    { provide: TRANSLOCO_MISSING_HANDLER, useClass: ScsMissingHandler },
    provideAppInitializer(() => inject(LanguageService).start()),
  ]);
}
