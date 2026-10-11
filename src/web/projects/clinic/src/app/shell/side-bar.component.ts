import { DOCUMENT } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import {
  AccessService,
  ClinicTraitsSource,
  InboxCountSource,
  LanguageService,
  PreferencesService,
} from 'core';
import { filter, map } from 'rxjs';
import { ScsIconComponent, ScsIconName, ScsTooltipDirective } from 'ui';
import { DESTINATIONS, Destination, NAV, PARENTS, childrenOf } from './destinations';

interface Entry {
  readonly key: string;
  readonly labelKey: string;
  readonly icon: ScsIconName;
  /** Its address; for a parent, null. */
  readonly link: string | null;
  readonly children: readonly Destination[];
  readonly count: boolean;
}

/** Below this width a person who never chose gets the bar folded (spec 006, FR-043). */
export const FOLD_BELOW = 1280;

/**
 * The console's side bar (design: Sidebar, Sidebar Nav Item; frames "Shell / Default",
 * "Sidebar Collapsed", "Sidebar Group Opened", "Limited Permissions"). It shows what the
 * person's role opens, in one order for everyone: an item whose view the person does not hold
 * is absent — never disabled — and a group left with nothing goes with its label (CL-SH-004).
 * It only hides; whether an address may be opened is decided by the route and by the platform.
 */
@Component({
  selector: 'app-side-bar',
  imports: [RouterLink, TranslocoPipe, ScsIconComponent, ScsTooltipDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    'data-testid': 'side-bar',
    '[attr.data-folded]': 'folded()',
    '[style.inline-size.px]': 'folded() ? 72 : 240',
  },
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      flex: none;
      padding: var(--scs-spacing-md) var(--scs-spacing-12);
      border-inline-end: 1px solid var(--scs-color-border-default);
      background: var(--scs-color-bg-surface);
      overflow: hidden auto;
      transition:
        inline-size 0.2s ease,
        padding 0.2s ease;
    }
    @media (prefers-reduced-motion: reduce) {
      :host,
      .item,
      .words,
      .label {
        transition: none !important;
      }
    }
    nav {
      display: flex;
      flex-direction: column;
      gap: var(--scs-spacing-xs);
    }
    .heading {
      display: flex;
      align-items: center;
      justify-content: space-between;
      min-block-size: 32px;
    }
    section:not(:first-child) .heading {
      min-block-size: 0;
    }
    :host([data-folded='true']) .heading {
      justify-content: center;
    }
    :host([data-folded='true']) .fold scs-icon {
      transform: scaleX(-1);
    }
    .label {
      margin: 0 0 var(--scs-spacing-xs);
      padding-block-start: var(--scs-spacing-xs);
      font-size: var(--scs-text-label);
      font-weight: 400;
      color: var(--scs-color-text-tertiary);
      white-space: nowrap;
    }
    section {
      display: flex;
      flex-direction: column;
      gap: var(--scs-spacing-xs);
    }
    /* Folded, a group's name gives way to a thin line: the groups stay apart, nothing is cut. */
    :host([data-folded='true']) .label {
      block-size: 1px;
      margin: var(--scs-spacing-sm) var(--scs-spacing-12);
      padding: 0;
      overflow: hidden;
      background: var(--scs-color-border-default);
      color: transparent;
    }
    :host([data-folded='true']) section:first-child .label {
      display: none;
    }
    .item {
      position: relative;
      display: flex;
      align-items: center;
      gap: var(--scs-spacing-12);
      inline-size: 100%;
      min-block-size: 40px;
      /* Design: Sidebar Nav Item — 12 at each side. */
      padding: 0 var(--scs-spacing-12);
      transition: padding 0.2s ease;
      overflow: hidden;
      border: 0;
      border-radius: var(--scs-radius-md);
      background: none;
      color: var(--scs-color-text-primary);
      font: inherit;
      font-size: var(--scs-text-body);
      font-weight: 500;
      text-align: start;
      text-decoration: none;
      cursor: pointer;
    }
    .item scs-icon {
      flex: none;
      color: var(--scs-color-icon-default);
    }
    .item:hover {
      background: var(--scs-color-bg-surface-secondary);
    }
    .item:focus-visible {
      outline: 2px solid var(--scs-color-brand-default);
      outline-offset: -2px;
    }
    .item[data-active='true'] {
      background: var(--scs-color-brand-subtle);
      color: var(--scs-color-brand-active);
      font-weight: 600;
    }
    .item[data-active='true'] scs-icon {
      color: var(--scs-color-brand-active);
    }
    .item[data-active='true']::before {
      content: '';
      position: absolute;
      inset-inline-start: 0;
      inset-block: 10px;
      inline-size: 3px;
      border-radius: var(--scs-radius-full);
      background: var(--scs-color-brand-default);
    }
    .words {
      flex: 1;
      min-inline-size: 0;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
      transition: opacity 0.15s ease;
    }
    .count {
      display: inline-grid;
      place-items: center;
      min-inline-size: 20px;
      block-size: 20px;
      padding-inline: var(--scs-spacing-6);
      border-radius: var(--scs-radius-full);
      background: var(--scs-color-bg-surface-secondary);
      color: var(--scs-color-text-secondary);
      font-size: var(--scs-text-caption);
      font-weight: 600;
    }
    .chevron {
      transition: transform 0.15s;
    }
    .item[aria-expanded='true'] .chevron {
      transform: rotate(180deg);
    }
    .children {
      display: flex;
      flex-direction: column;
      gap: var(--scs-spacing-xs);
    }
    /* Design: Sidebar, sub-item — 36 high, set in by 44, the words a size smaller. */
    .children .item {
      min-block-size: 36px;
      padding-inline-start: 44px;
      font-size: var(--scs-text-body-sm);
      font-weight: 500;
    }
    .children .item[data-active='true'] {
      font-weight: 600;
    }
    /* Design: "Shell / Collapsed" — 40 squares in the 72 bar; the open page is a white square
       with a hairline, and has no bar beside it. */
    :host([data-folded='true']) {
      padding-inline: var(--scs-spacing-md);
    }
    :host([data-folded='true']) .item {
      padding: 0 10px;
    }
    :host([data-folded='true']) .item[data-active='true'] {
      background: var(--scs-color-bg-surface);
      box-shadow: inset 0 0 0 1px var(--scs-color-border-default);
    }
    :host([data-folded='true']) .item[data-active='true']::before {
      display: none;
    }
    /* Folded: the words fade and are cut by the narrow bar; they stay for assistive technology. */
    :host([data-folded='true']) .words {
      opacity: 0;
    }
    :host([data-folded='true']) .count,
    :host([data-folded='true']) .chevron,
    :host([data-folded='true']) .children {
      display: none;
    }
    @media (prefers-reduced-motion: reduce) {
      .chevron {
        transition: none;
      }
    }
  `,
  template: `
    <nav [attr.aria-label]="'app.nav.label' | transloco">
      @for (group of groups(); track group.group) {
        <section [attr.data-group]="group.group">
          <!-- The fold button is at the top, beside the first group's name, where it is seen:
               the owner's decision; the design draws it at the foot (validation.md). -->
          <div class="heading">
            <p class="label">{{ group.labelKey | transloco }}</p>
            @if ($first) {
              <button
                type="button"
                class="scs-icon-button fold"
                data-testid="side-bar-fold"
                [attr.aria-pressed]="folded()"
                [attr.aria-label]="(folded() ? 'app.nav.unfold' : 'app.nav.fold') | transloco"
                [scsTooltip]="(folded() ? 'app.nav.unfold' : 'app.nav.fold') | transloco"
                (click)="setFolded(!folded())"
              >
                <scs-icon name="chevron-start" [size]="16" />
              </button>
            }
          </div>
          @for (entry of group.entries; track entry.key) {
            @if (entry.link !== null) {
              <a
                class="item"
                [routerLink]="entry.link"
                [attr.data-testid]="'nav-' + entry.key"
                [attr.data-active]="isActive(entry.link)"
                [attr.aria-current]="isActive(entry.link) ? 'page' : null"
                [scsTooltip]="folded() ? (entry.labelKey | transloco) : ''"
              >
                <scs-icon [name]="entry.icon" [size]="20" />
                <span class="words">{{ entry.labelKey | transloco }}</span>
                @if (entry.count && inbox(); as unread) {
                  <span class="count" data-testid="nav-inbox-count">{{ digits(unread) }}</span>
                }
              </a>
            } @else {
              <button
                type="button"
                class="item"
                [attr.data-testid]="'nav-' + entry.key"
                [attr.aria-expanded]="isOpen(entry.key)"
                [attr.data-active]="!isOpen(entry.key) && holdsActive(entry)"
                [scsTooltip]="folded() ? (entry.labelKey | transloco) : ''"
                (click)="toggle(entry.key)"
              >
                <scs-icon [name]="entry.icon" [size]="20" />
                <span class="words">{{ entry.labelKey | transloco }}</span>
                <scs-icon class="chevron" name="chevron-down" [size]="16" />
              </button>
              @if (isOpen(entry.key)) {
                <div class="children" role="group" [attr.aria-label]="entry.labelKey | transloco">
                  @for (child of entry.children; track child.key) {
                    <a
                      class="item"
                      [routerLink]="'/' + child.path"
                      [attr.data-testid]="'nav-' + child.key"
                      [attr.data-active]="isActive('/' + child.path)"
                      [attr.aria-current]="isActive('/' + child.path) ? 'page' : null"
                    >
                      <scs-icon [name]="child.icon" [size]="20" />
                      <span class="words">{{ child.labelKey | transloco }}</span>
                    </a>
                  }
                </div>
              }
            }
          }
        </section>
      }
    </nav>
  `,
})
export class SideBarComponent {
  private readonly access = inject(AccessService);
  private readonly traits = inject(ClinicTraitsSource);
  private readonly preferences = inject(PreferencesService);
  private readonly language = inject(LanguageService);
  private readonly router = inject(Router);
  private readonly window = inject(DOCUMENT).defaultView;

  protected readonly inbox = inject(InboxCountSource).unread;

  private readonly url = toSignal(
    this.router.events.pipe(
      filter((event) => event instanceof NavigationEnd),
      map(() => this.router.url),
    ),
    { initialValue: this.router.url },
  );

  private readonly narrow = signal(this.isNarrow());
  /**
   * The parent the person opened or closed by hand, and on which page they did. One parent is
   * open at a time: opening one closes the other. The choice holds until they go to another
   * page; then the parent of the open page is the open one again.
   */
  private readonly chosen = signal<{ at: string; key: string | null } | null>(null);

  constructor() {
    this.window?.addEventListener('resize', () => this.narrow.set(this.isNarrow()));
  }

  /** The person's saved choice; with none, the width of the screen decides. */
  readonly folded = computed(() => this.preferences.sidebarFolded() ?? this.narrow());

  /** What the person may open, in the one order everyone sees; empty groups are gone. */
  protected readonly groups = computed(() =>
    NAV.map((group) => ({
      group: group.group,
      labelKey: group.labelKey,
      entries: group.entries.map((key) => this.entry(key)).filter((entry) => entry !== null),
    })).filter((group) => group.entries.length > 0),
  );

  protected isActive(link: string): boolean {
    const path = this.url().split(/[?#]/)[0];
    return link === '/' ? path === '/' : path === link || path.startsWith(link + '/');
  }

  protected holdsActive(entry: Entry): boolean {
    return entry.children.some((child) => this.isActive('/' + child.path));
  }

  /** A parent is open when pressed open, or when the open page is one of its children. */
  protected isOpen(key: string): boolean {
    if (this.folded()) {
      return false;
    }
    const chosen = this.chosen();
    if (chosen !== null && chosen.at === this.url()) {
      return chosen.key === key;
    }
    return childrenOf(key).some((child) => this.isActive('/' + child.path));
  }

  protected toggle(key: string): void {
    if (this.folded()) {
      // From the folded bar a group's icon unfolds the bar with that group open (CL-SH-013).
      this.setFolded(false);
      this.mark(key, true);
      return;
    }
    this.mark(key, !this.isOpen(key));
  }

  /** Folds or unfolds the bar and keeps the choice. */
  setFolded(folded: boolean): void {
    this.preferences.setSidebarFolded(folded);
  }

  /** A count in the digits of the page's language. */
  protected digits(value: number): string {
    return new Intl.NumberFormat(this.language.language() === 'ar' ? 'ar-EG-u-nu-arab' : 'en-GB').format(value);
  }

  private mark(key: string, open: boolean): void {
    this.chosen.set({ at: this.url(), key: open ? key : null });
  }

  private entry(key: string): Entry | null {
    const parent = PARENTS.find((candidate) => candidate.key === key);
    if (parent !== undefined) {
      const children = childrenOf(parent.key).filter((child) => this.opens(child));
      return children.length === 0
        ? null
        : { key, labelKey: parent.labelKey, icon: parent.icon, link: null, children, count: false };
    }

    const found = DESTINATIONS.find((candidate) => candidate.key === key);
    if (found === undefined || !this.opens(found)) {
      return null;
    }
    return {
      key,
      labelKey: found.labelKey,
      icon: found.icon,
      link: '/' + found.path,
      children: [],
      count: key === 'inbox',
    };
  }

  private opens(destination: Destination): boolean {
    if (destination.needs === 'dental' && !this.traits.dental()) {
      return false;
    }
    return destination.permission === null || this.access.holds(destination.permission);
  }

  private isNarrow(): boolean {
    return (this.window?.innerWidth ?? FOLD_BELOW) < FOLD_BELOW;
  }
}
