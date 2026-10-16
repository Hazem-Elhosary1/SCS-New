import { Injectable, computed, inject, signal } from '@angular/core';
import { LanguageService } from 'core';
import { GALLERY_WORDS } from './gallery-words';

/** The gallery's two switches: the page's language (the console's own) and the look shown. */
@Injectable({ providedIn: 'root' })
export class GalleryState {
  private readonly languages = inject(LanguageService);

  readonly language = this.languages.language;
  readonly look = signal<'ops' | 'clinic'>('ops');
  readonly words = computed(() => GALLERY_WORDS[this.language()]);

  useLanguage(language: 'ar' | 'en'): void {
    this.languages.use(language);
  }
}
