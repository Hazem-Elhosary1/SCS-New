import { NgComponentOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, Type, computed, effect, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { map } from 'rxjs';
import { ClinicLookComponent } from './clinic-look.component';
import { GALLERY_SECTIONS, GallerySection } from './gallery-sections';
import { GalleryState } from './gallery-state';

/**
 * The design components' gallery (spec 095, `contracts/gallery.md`): every component of the
 * operators' design file in every drawn state, in both languages, in the operators' look or — for
 * the shared ones — the clinic's. Development only; outside the console's frame; no sign-in.
 */
@Component({
  selector: 'app-gallery-page',
  imports: [NgComponentOutlet, RouterLink, ClinicLookComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: block;
      min-block-size: 100vh;
      background: var(--scs-color-bg-canvas);
      color: var(--scs-color-text-primary);
    }
    .bar {
      position: sticky;
      inset-block-start: 0;
      z-index: 10;
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--scs-spacing-md);
      padding: var(--scs-spacing-12) var(--scs-spacing-lg);
      background: var(--scs-color-bg-surface);
      border-block-end: 1px solid var(--scs-color-border-default);
    }
    h1 {
      margin: 0;
      font-size: var(--scs-text-heading-sm);
      font-weight: 600;
    }
    .note {
      flex-basis: 100%;
      margin: 0;
      font-size: var(--scs-text-body-sm);
      color: var(--scs-color-text-secondary);
    }
    .switch {
      display: inline-flex;
      align-items: center;
      gap: var(--scs-spacing-sm);
      font-size: var(--scs-text-body-sm);
      color: var(--scs-color-text-secondary);
    }
    .switch button {
      min-block-size: 40px;
      min-inline-size: 40px;
      padding: 0 var(--scs-spacing-12);
      border: 1px solid var(--scs-color-border-control, var(--scs-color-border-default));
      border-radius: var(--scs-radius-sm);
      background: var(--scs-color-bg-surface);
      color: var(--scs-color-text-primary);
      font: inherit;
      cursor: pointer;
    }
    .switch button[aria-pressed='true'] {
      border-color: var(--scs-color-brand-default);
      background: var(--scs-color-brand-subtle);
      color: var(--scs-color-brand-default);
      font-weight: 600;
    }
    nav {
      display: flex;
      flex-wrap: wrap;
      gap: var(--scs-spacing-xs) var(--scs-spacing-sm);
      padding: var(--scs-spacing-12) var(--scs-spacing-lg);
      font-size: var(--scs-text-body-sm);
    }
    nav a {
      color: var(--scs-color-text-link);
    }
    nav a[aria-current='page'] {
      font-weight: 600;
      color: var(--scs-color-text-primary);
    }
    main {
      display: grid;
      gap: var(--scs-spacing-lg);
      padding: 0 var(--scs-spacing-lg) var(--scs-spacing-3xl);
    }
    section {
      padding: var(--scs-spacing-lg);
      border: 1px solid var(--scs-color-border-default);
      border-radius: var(--scs-radius-md);
      background: var(--scs-color-bg-surface);
    }
    section > header {
      display: flex;
      flex-wrap: wrap;
      align-items: baseline;
      gap: var(--scs-spacing-sm);
      margin-block-end: var(--scs-spacing-md);
    }
    h2 {
      margin: 0;
      font-size: var(--scs-text-title-sm);
      font-weight: 600;
    }
    .node {
      font-family: ui-monospace, Consolas, monospace;
      font-size: var(--scs-text-label);
      color: var(--scs-color-text-tertiary);
    }
    .empty {
      margin: 0;
      color: var(--scs-color-text-tertiary);
    }
    @media (max-width: 720px) {
      .bar,
      nav {
        padding-inline: var(--scs-spacing-md);
      }
      main {
        padding-inline: var(--scs-spacing-md);
      }
      section {
        padding: var(--scs-spacing-md);
      }
    }
  `,
  template: `
    <div class="bar" data-testid="gallery">
      <h1>{{ words().title }}</h1>
      <div class="switch" role="group" [attr.aria-label]="words().language" data-testid="gallery-language">
        <span>{{ words().language }}</span>
        <button type="button" lang="ar" [attr.aria-pressed]="state.language() === 'ar'" (click)="state.useLanguage('ar')">
          {{ arabic }}
        </button>
        <button type="button" lang="en" [attr.aria-pressed]="state.language() === 'en'" (click)="state.useLanguage('en')">
          {{ english }}
        </button>
      </div>
      <div class="switch" role="group" [attr.aria-label]="words().look" data-testid="gallery-look">
        <span>{{ words().look }}</span>
        <button type="button" [attr.aria-pressed]="state.look() === 'ops'" (click)="state.look.set('ops')">
          {{ words().operators }}
        </button>
        <button type="button" [attr.aria-pressed]="state.look() === 'clinic'" (click)="state.look.set('clinic')">
          {{ words().clinic }}
        </button>
      </div>
      <p class="note">{{ state.look() === 'clinic' ? words().onlyShared : words().note }}</p>
    </div>

    <nav [attr.aria-label]="words().sections">
      <a routerLink="/dev/components" [attr.aria-current]="page() ? null : 'page'">{{ words().all }}</a>
      @for (section of sections; track section.id) {
        <a
          [routerLink]="['/dev/components', section.id]"
          [attr.aria-current]="page() === section.id ? 'page' : null"
          data-scs-label
          >{{ section.title }}</a
        >
      }
    </nav>

    @if (state.look() === 'clinic') {
      <app-gallery-clinic-look />
    }
    <main [attr.data-scs-look]="state.look()">
      @for (section of shown(); track section.id) {
        <section [attr.data-testid]="'section-' + section.id" [attr.aria-labelledby]="'title-' + section.id">
          <header>
            <h2 [id]="'title-' + section.id" data-scs-label>{{ section.title }}</h2>
            @if (section.node) {
              <span class="node" data-scs-label>{{ section.node }}</span>
            }
          </header>
          @if (loaded()[section.id]; as content) {
            <ng-container *ngComponentOutlet="content" />
          } @else {
            <p class="empty">{{ words().notBuilt }}</p>
          }
        </section>
      }
    </main>
  `,
})
export class GalleryPage {
  protected readonly state = inject(GalleryState);
  protected readonly words = this.state.words;
  protected readonly sections = GALLERY_SECTIONS;
  // The two names of the languages are the languages' own, as on every language switch.
  protected readonly arabic = 'العربية';
  protected readonly english = 'English';

  /** `/dev/components/<page>` shows one section; `/dev/components`, all of them. */
  protected readonly page = toSignal(inject(ActivatedRoute).paramMap.pipe(map((params) => params.get('page'))), {
    initialValue: null,
  });

  protected readonly shown = computed<readonly GallerySection[]>(() => {
    const id = this.page();
    return id ? this.sections.filter((section) => section.id === id) : this.sections;
  });

  protected readonly loaded = signal<Record<string, Type<unknown>>>({});

  constructor() {
    effect(() => {
      for (const section of this.shown()) {
        if (section.load && !this.loaded()[section.id]) {
          void section.load().then((content) => this.loaded.update((all) => ({ ...all, [section.id]: content })));
        }
      }
    });
  }
}
