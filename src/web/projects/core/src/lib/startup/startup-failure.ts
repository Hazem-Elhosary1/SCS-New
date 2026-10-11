/**
 * Shown when the app cannot start at all (for example `config.json` is missing). Nothing else
 * is available at that point, not even translations, so the two sentences are written here in
 * both languages. This is the only place in the front ends where that is allowed.
 */
export function renderStartupFailure(error: unknown, doc: Document = document): void {
  console.error(error);

  const box = doc.createElement('div');
  box.setAttribute('role', 'alert');
  box.setAttribute('data-scs-startup-failure', '');
  box.style.cssText =
    'max-width:32rem;margin:4rem auto;padding:1.5rem;font-family:system-ui;text-align:center';

  const arabic = doc.createElement('p');
  arabic.lang = 'ar';
  arabic.dir = 'rtl';
  arabic.textContent = 'تعذّر تشغيل الصفحة. تُرجى إعادة المحاولة بعد قليل.';

  const english = doc.createElement('p');
  english.lang = 'en';
  english.dir = 'ltr';
  english.textContent = 'The page could not start. Please try again shortly.';

  box.append(arabic, english);
  doc.body.replaceChildren(box);
}
