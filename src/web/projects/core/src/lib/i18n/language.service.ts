import { DOCUMENT, Injectable, inject, signal } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';

export type ScsLanguage = 'ar' | 'en';

export const SCS_LANGUAGES: readonly ScsLanguage[] = ['ar', 'en'];
export const DEFAULT_LANGUAGE: ScsLanguage = 'ar';

const STORAGE_KEY = 'scs.language';

export function isScsLanguage(value: unknown): value is ScsLanguage {
  return value === 'ar' || value === 'en';
}

export function directionOf(language: ScsLanguage): 'rtl' | 'ltr' {
  return language === 'ar' ? 'rtl' : 'ltr';
}

/** The language remembered in this browser, or Arabic. Spec 003 ties it to the user. */
export function rememberedLanguage(): ScsLanguage {
  try {
    const stored = globalThis.localStorage?.getItem(STORAGE_KEY);
    return isScsLanguage(stored) ? stored : DEFAULT_LANGUAGE;
  } catch {
    return DEFAULT_LANGUAGE;
  }
}

/**
 * The active language (SH-002). Switching changes every visible text and the page direction
 * at once, without a reload, and is remembered for the next visit.
 */
@Injectable({ providedIn: 'root' })
export class LanguageService {
  private readonly transloco = inject(TranslocoService);
  private readonly document = inject(DOCUMENT);
  private readonly active = signal<ScsLanguage>(DEFAULT_LANGUAGE);

  readonly language = this.active.asReadonly();

  /** Applies the remembered language and loads both languages, so a missing text can fall back. */
  async start(): Promise<void> {
    this.use(rememberedLanguage());
    await Promise.all(
      SCS_LANGUAGES.map(
        (language) =>
          new Promise<void>((resolve) =>
            this.transloco.load(language).subscribe({ complete: resolve, error: () => resolve() }),
          ),
      ),
    );
  }

  use(language: ScsLanguage): void {
    this.active.set(language);
    this.transloco.setActiveLang(language);

    const root = this.document.documentElement;
    root.lang = language;
    root.dir = directionOf(language);

    try {
      globalThis.localStorage?.setItem(STORAGE_KEY, language);
    } catch {
      // Storage can be unavailable (private mode); the choice then lasts for this visit only.
    }
  }

  toggle(): void {
    this.use(this.active() === 'ar' ? 'en' : 'ar');
  }
}
