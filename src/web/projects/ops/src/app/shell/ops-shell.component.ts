import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  ElementRef,
  HostListener,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { OnlineService } from 'core';
import { ScsIconComponent } from 'ui';
import { OpsAuthService } from '../auth/ops-auth.service';
import { OPS_SECTIONS } from './sections';

/** The first letters of the first two words of a name: what the round mark shows. */
export function initialsOf(name: string): string {
  return name
    .trim()
    .split(/\s+/)
    .slice(0, 2)
    .map((word) => [...word][0] ?? '')
    .join('')
    .toLocaleUpperCase();
}

/**
 * The frame of the operators console (spec 010, FR-047 to FR-051; design pages "Navbar" and
 * "Sidebar Nav Item", and the frame around "Template — Profile"): a top bar with the console's
 * name, the operator's name and role and the account menu; a side bar that lists only the
 * sections that exist. It is not the clinic's shell: nothing here knows of a clinic.
 */
@Component({
  selector: 'app-ops-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, TranslocoPipe, ScsIconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: grid;
      grid-template-rows: 64px 1fr;
      min-block-size: 100dvh;
      background: var(--scs-color-bg-canvas);
    }
    header {
      display: flex;
      align-items: center;
      gap: var(--scs-spacing-md);
      padding-inline: var(--scs-spacing-lg);
      border-block-end: 1px solid var(--scs-color-border-default);
      background: var(--scs-color-bg-surface);
    }
    .brand {
      display: flex;
      align-items: center;
      gap: var(--scs-spacing-12);
      font-weight: 600;
      text-decoration: none;
      color: var(--scs-color-text-primary);
    }
    .mark {
      display: grid;
      place-items: center;
      inline-size: 36px;
      block-size: 36px;
      border-radius: var(--scs-radius-md);
      background: var(--scs-color-brand-default);
      color: var(--scs-color-brand-on-brand);
      font-weight: 700;
    }
    .space {
      flex: 1;
    }
    .who {
      display: flex;
      align-items: center;
      gap: var(--scs-spacing-12);
      padding: var(--scs-spacing-xs) var(--scs-spacing-sm);
      border: 0;
      border-radius: var(--scs-radius-md);
      background: none;
      color: inherit;
      font: inherit;
      text-align: start;
      cursor: pointer;
    }
    .who:hover,
    .who[aria-expanded='true'] {
      background: var(--scs-color-bg-surface-secondary);
    }
    .who:focus-visible,
    .menu button:focus-visible,
    nav a:focus-visible,
    .opener:focus-visible {
      outline: 2px solid var(--scs-color-brand-default);
      outline-offset: 2px;
    }
    .names {
      display: flex;
      flex-direction: column;
      max-inline-size: 220px;
    }
    .names span {
      overflow: hidden;
      white-space: nowrap;
      text-overflow: ellipsis;
    }
    .name {
      font-weight: 600;
    }
    .role {
      font-size: var(--scs-text-caption);
      color: var(--scs-color-text-tertiary);
    }
    .initials {
      display: grid;
      place-items: center;
      flex: none;
      inline-size: 36px;
      block-size: 36px;
      border-radius: var(--scs-radius-full);
      background: var(--scs-color-brand-subtle);
      color: var(--scs-color-brand-default);
      font-weight: 600;
    }
    .account {
      position: relative;
    }
    .menu {
      position: absolute;
      inset-block-start: calc(100% + 6px);
      inset-inline-end: 0;
      z-index: 20;
      display: flex;
      flex-direction: column;
      min-inline-size: 200px;
      padding: var(--scs-spacing-xs);
      border: 1px solid var(--scs-color-border-default);
      border-radius: var(--scs-radius-md);
      background: var(--scs-color-bg-surface);
      box-shadow: 0 8px 24px rgb(0 0 0 / 12%);
    }
    .menu button {
      display: flex;
      align-items: center;
      gap: var(--scs-spacing-sm);
      padding: var(--scs-spacing-sm) var(--scs-spacing-12);
      border: 0;
      border-radius: var(--scs-radius-sm);
      background: none;
      color: inherit;
      font: inherit;
      text-align: start;
      cursor: pointer;
    }
    .menu button:hover {
      background: var(--scs-color-bg-surface-secondary);
    }
    .menu .out {
      color: var(--scs-color-feedback-danger-text, var(--scs-color-feedback-danger-icon));
    }
    .menu hr {
      inline-size: 100%;
      margin: var(--scs-spacing-xs) 0;
      border: 0;
      border-block-start: 1px solid var(--scs-color-border-default);
    }
    .body {
      display: grid;
      grid-template-columns: 240px minmax(0, 1fr);
      min-block-size: 0;
    }
    nav {
      display: flex;
      flex-direction: column;
      gap: var(--scs-spacing-xs);
      padding: var(--scs-spacing-md);
      border-inline-end: 1px solid var(--scs-color-border-default);
      background: var(--scs-color-bg-surface);
    }
    nav a {
      display: flex;
      align-items: center;
      gap: var(--scs-spacing-12);
      padding: var(--scs-spacing-sm) var(--scs-spacing-12);
      border-radius: var(--scs-radius-md);
      color: var(--scs-color-text-primary);
      text-decoration: none;
    }
    nav a:hover {
      background: var(--scs-color-bg-surface-secondary);
    }
    nav a.active {
      background: var(--scs-color-brand-subtle);
      color: var(--scs-color-brand-default);
      font-weight: 600;
    }
    main {
      min-inline-size: 0;
      padding: var(--scs-spacing-lg);
    }
    .offline {
      margin: 0 0 var(--scs-spacing-md);
      padding: var(--scs-spacing-sm) var(--scs-spacing-md);
      border-radius: var(--scs-radius-md);
      background: var(--scs-color-feedback-warning-bg, var(--scs-color-bg-surface-secondary));
    }
    .opener {
      display: none;
      place-items: center;
      inline-size: 36px;
      block-size: 36px;
      border: 1px solid var(--scs-color-border-default);
      border-radius: var(--scs-radius-md);
      background: var(--scs-color-bg-surface);
      color: inherit;
      cursor: pointer;
    }
    @media (max-width: 1023px) {
      .opener {
        display: grid;
      }
      .body {
        grid-template-columns: minmax(0, 1fr);
      }
      nav {
        display: none;
      }
      nav.open {
        display: flex;
        position: absolute;
        inset-block: 64px 0;
        inset-inline-start: 0;
        z-index: 15;
        inline-size: 240px;
        box-shadow: 0 8px 24px rgb(0 0 0 / 12%);
      }
      .names {
        display: none;
      }
    }
  `,
  template: `
    <header data-testid="ops-top-bar">
      <button
        type="button"
        class="opener"
        data-testid="ops-nav-opener"
        [attr.aria-label]="'app.shell.sections' | transloco"
        [attr.aria-expanded]="navOpen()"
        (click)="navOpen.set(!navOpen())"
      >
        <scs-icon name="menu" [size]="18" />
      </button>

      <a class="brand" routerLink="/" data-testid="ops-brand">
        <span class="mark" aria-hidden="true">{{ mark }}</span>
        <span>{{ 'app.shell.name' | transloco }}</span>
      </a>

      <span class="space"></span>

      @if (operator(); as who) {
        <div class="account">
          <button
            #opener
            type="button"
            class="who"
            data-testid="ops-account"
            aria-haspopup="menu"
            [attr.aria-expanded]="menuOpen()"
            [attr.aria-label]="'app.shell.account' | transloco"
            (click)="toggleMenu()"
            (keydown.arrowdown)="openMenu($event)"
          >
            <span class="names">
              <span class="name" dir="auto" data-testid="ops-operator-name" [attr.title]="who.fullName">{{
                who.fullName
              }}</span>
              <span class="role" data-testid="ops-operator-role" [attr.title]="who.roleName">{{ who.roleName }}</span>
            </span>
            <span class="initials" aria-hidden="true" data-testid="ops-initials">{{ initials() }}</span>
          </button>

          @if (menuOpen()) {
            <div class="menu" role="menu" tabindex="-1" data-testid="ops-account-menu" (keydown)="menuKey($event)">
              <button type="button" role="menuitem" data-testid="ops-menu-profile" (click)="go('/profile')">
                <scs-icon name="user" [size]="16" />
                <span>{{ 'app.shell.menu.profile' | transloco }}</span>
              </button>
              <hr />
              <button type="button" role="menuitem" class="out" data-testid="ops-menu-sign-out" (click)="signOut()">
                <scs-icon name="sign-out" [size]="16" />
                <span>{{ 'app.shell.menu.signOut' | transloco }}</span>
              </button>
            </div>
          }
        </div>
      }
    </header>

    <div class="body">
      <nav data-testid="ops-side-bar" [class.open]="navOpen()" [attr.aria-label]="'app.shell.sections' | transloco">
        @for (section of sections; track section.path) {
          <a
            [routerLink]="section.path"
            routerLinkActive="active"
            [routerLinkActiveOptions]="{ exact: section.path === '/' }"
            [attr.data-testid]="'ops-nav-' + section.key"
            (click)="navOpen.set(false)"
          >
            <scs-icon [name]="section.icon" [size]="18" />
            <span>{{ 'app.shell.nav.' + section.key | transloco }}</span>
          </a>
        }
      </nav>

      <main>
        @if (!online.online()) {
          <p class="offline" role="status" data-testid="ops-offline">{{ 'app.shell.offline' | transloco }}</p>
        }
        <router-outlet />
      </main>
    </div>
  `,
})
export class OpsShellComponent {
  private readonly auth = inject(OpsAuthService);
  private readonly router = inject(Router);
  private readonly changes = inject(ChangeDetectorRef);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  protected readonly online = inject(OnlineService);

  /** The console's mark: the first letter of the product's name, in no language. */
  protected readonly mark = 'S';

  protected readonly sections = OPS_SECTIONS;
  protected readonly menuOpen = signal(false);
  protected readonly navOpen = signal(false);

  protected readonly operator = computed(() => this.auth.session()?.operator ?? null);
  protected readonly initials = computed(() => initialsOf(this.operator()?.fullName ?? ''));

  private readonly opener = viewChild<ElementRef<HTMLButtonElement>>('opener');

  protected toggleMenu(): void {
    this.menuOpen.set(!this.menuOpen());
  }

  protected openMenu(event: Event): void {
    event.preventDefault();
    this.menuOpen.set(true);
    // Drawn at once, so the first item has the focus before the next key arrives: Enter
    // straight after the arrow chooses it, and never closes the menu again.
    this.changes.detectChanges();
    this.items()[0]?.focus();
  }

  /** Arrow keys move between the items; Escape closes and gives the focus back. */
  protected menuKey(event: KeyboardEvent): void {
    const items = this.items();
    const at = items.indexOf(event.target as HTMLButtonElement);
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault();
      const step = event.key === 'ArrowDown' ? 1 : -1;
      items[(at + step + items.length) % items.length]?.focus();
    } else if (event.key === 'Escape') {
      this.closeMenu(true);
    }
  }

  @HostListener('document:keydown.escape')
  protected escape(): void {
    if (this.menuOpen()) {
      this.closeMenu(true);
    }
    this.navOpen.set(false);
  }

  @HostListener('document:pointerdown', ['$event'])
  protected outside(event: Event): void {
    const target = event.target as Node;
    if (this.menuOpen() && !this.host.nativeElement.querySelector('.account')?.contains(target)) {
      this.closeMenu(false);
    }
  }

  protected async go(path: string): Promise<void> {
    this.closeMenu(false);
    await this.router.navigateByUrl(path);
  }

  protected async signOut(): Promise<void> {
    this.closeMenu(false);
    await this.auth.signOut();
  }

  private closeMenu(focusOpener: boolean): void {
    this.menuOpen.set(false);
    if (focusOpener) {
      this.opener()?.nativeElement.focus();
    }
  }

  private items(): HTMLButtonElement[] {
    return [...this.host.nativeElement.querySelectorAll<HTMLButtonElement>('.menu button')];
  }
}
