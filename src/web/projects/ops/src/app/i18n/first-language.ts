/**
 * Before sign-in the console speaks the language last chosen in this browser, else the
 * browser's own, else Arabic (spec 010, FR-054). The shared language service knows the first
 * and the last of these; this gives it the middle one, once, before the application starts:
 * a browser that has chosen nothing and prefers English starts in English.
 */
const STORAGE_KEY = 'scs.language';

/** The first of the browser's preferred languages that the console speaks, or none. */
export function browserLanguage(preferred: readonly string[]): 'ar' | 'en' | null {
  for (const tag of preferred) {
    const language = tag.toLowerCase().split('-')[0];
    if (language === 'ar' || language === 'en') {
      return language;
    }
  }
  return null;
}

export function startInBrowserLanguage(
  storage: Pick<Storage, 'getItem' | 'setItem'> | undefined = globalThis.localStorage,
  preferred: readonly string[] = globalThis.navigator?.languages ?? [],
): void {
  try {
    if (storage && storage.getItem(STORAGE_KEY) === null) {
      const language = browserLanguage(preferred);
      if (language !== null) {
        storage.setItem(STORAGE_KEY, language);
      }
    }
  } catch {
    // Storage can be unavailable (private mode): the console then starts in Arabic.
  }
}
