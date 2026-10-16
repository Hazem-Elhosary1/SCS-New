import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  ElementRef,
  HostListener,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { OnlineService } from 'core';
import { filter } from 'rxjs';
import { ScsIconComponent, ScsNavItemComponent, ScsNavbarComponent, ScsToastOutletComponent } from 'ui';
import { OpsAuthService } from '../auth/ops-auth.service';
import { OpsAccess } from '../access/ops-access.service';
import { OPS_SECTIONS } from './sections';

/** Below this width the side bar goes behind a control and the top bar shows only the initials. */
const FOLDED = '(max-width: 1023px)';

/**
 * The frame of the operators console (spec 010, FR-047 to FR-051; design pages "Navbar" and
 * "Sidebar Nav Item", and the frame around "Template — Profile"), drawn with the shared parts since
 * spec 095: `scs-navbar` across the top — the console's name, the operator's name, role and initials
 * as the button of the account menu — and an `scs-nav-item` for each section that exists in the side
 * bar. It is not the clinic's shell: nothing here knows of a clinic. The bell (`scs-notification-bell`)
 * joins the top bar when spec 093 brings notices; until then there is none, as spec 011 left it.
 */
@Component({
  selector: 'app-ops-shell',
  imports: [RouterOutlet, TranslocoPipe, ScsIconComponent, ScsNavbarComponent, ScsNavItemComponent, ScsToastOutletComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `
    :host {
      display: grid;
      grid-template-rows: 64px 1fr;
      min-block-size: 100dvh;
      background: var(--scs-color-bg-canvas);
    }
    .opener {
      display: none;
      inline-size: max(var(--scs-touch, 0px), 36px);
      block-size: max(var(--scs-touch, 0px), 36px);
      color: inherit;
    }
    .opener:focus-visible {
      outline: 2px solid var(--scs-color-brand-default);
      outline-offset: 2px;
    }
    .account-menu {
      position: absolute;
      inset-block-start: calc(100% + 2px);
      inset-inline-end: 0;
      z-index: 20;
      min-inline-size: 200px;
    }
    .account-menu hr {
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
    /* 240 wide, its items 216: 12 at the sides, 16 above, 8 between (Getting Started, 4:2). */
    nav {
      display: flex;
      flex-direction: column;
      gap: var(--scs-spacing-sm);
      padding: var(--scs-spacing-md) var(--scs-spacing-12);
      border-inline-end: 1px solid var(--scs-color-border-default);
      background: var(--scs-color-bg-surface);
    }
    main {
      position: relative;
      min-inline-size: 0;
      padding: var(--scs-spacing-lg);
    }
    scs-toast-outlet {
      position: absolute;
      inset-inline-end: var(--scs-spacing-lg);
      z-index: 5;
    }
    .offline {
      margin: 0 0 var(--scs-spacing-md);
      padding: var(--scs-spacing-sm) var(--scs-spacing-md);
      border-radius: var(--scs-radius-md);
      background: var(--scs-color-feedback-warning-bg, var(--scs-color-bg-surface-secondary));
    }
    @media (max-width: 1023px) {
      .opener {
        display: inline-grid;
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
    }
  `,
  template: `
    <scs-navbar
      testId="ops"
      data-testid="ops-top-bar"
      [name]="'app.shell.name' | transloco"
      [mark]="mark"
      [person]="operator()?.fullName ?? ''"
      [role]="roleName()"
      [accountLabel]="'app.shell.account' | transloco"
      [accountOpen]="menuOpen()"
      [compact]="folded()"
      (account)="accountPressed($event)"
    >
      <button
        scsNavbarStart
        type="button"
        class="scs-icon-button opener"
        data-testid="ops-nav-opener"
        [attr.aria-label]="'app.shell.sections' | transloco"
        [attr.aria-expanded]="navOpen()"
        (click)="navOpen.set(!navOpen())"
      >
        <scs-icon name="menu" [size]="18" />
      </button>

      @if (menuOpen()) {
        <div scsNavbarMenu class="scs-menu account-menu" role="menu" tabindex="-1" data-testid="ops-account-menu" (keydown)="menuKey($event)">
          <button type="button" role="menuitem" class="scs-menu-item" data-testid="ops-menu-profile" (click)="go('/profile')">
            <scs-icon name="user" [size]="16" />
            <span>{{ 'app.shell.menu.profile' | transloco }}</span>
          </button>
          <hr />
          <button type="button" role="menuitem" class="scs-menu-item" data-danger="true" data-testid="ops-menu-sign-out" (click)="signOut()">
            <scs-icon name="sign-out" [size]="16" />
            <span>{{ 'app.shell.menu.signOut' | transloco }}</span>
          </button>
        </div>
      }
    </scs-navbar>

    <div class="body">
      <nav data-testid="ops-side-bar" [class.open]="navOpen()" [attr.aria-label]="'app.shell.sections' | transloco">
        @for (section of sections(); track section.path) {
          <scs-nav-item
            [label]="'app.shell.nav.' + section.key | transloco"
            [icon]="section.icon"
            [link]="section.path"
            [exact]="section.path === '/'"
            [testId]="'ops-nav-' + section.key"
            (click)="navOpen.set(false)"
          />
        }
      </nav>

      <main>
        <scs-toast-outlet [closeLabel]="'app.operators.list.close' | transloco" />
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

  private readonly access = inject(OpsAccess);

  /** Only the sections the operator's role opens (spec 011, FR-006). */
  protected readonly sections = computed(() =>
    OPS_SECTIONS.filter((section) => section.permission === undefined || this.access.can(section.permission)),
  );
  protected readonly roleName = this.access.roleName;
  protected readonly menuOpen = signal(false);
  protected readonly navOpen = signal(false);
  /** The frame is folded: the side bar behind its control, only the initials in the top bar. */
  protected readonly folded = signal(false);

  protected readonly operator = computed(() => this.auth.session()?.operator ?? null);

  private readonly navbar = viewChild.required(ScsNavbarComponent);

  constructor() {
    // What the role holds is read again on every page of the console (spec 011, FR-008): a
    // changed role shows in the side bar without a reload.
    this.router.events
      .pipe(
        filter((event) => event instanceof NavigationEnd),
        takeUntilDestroyed(),
      )
      .subscribe(() => void this.auth.refreshIfStale());

    if (typeof matchMedia === 'function') {
      const width = matchMedia(FOLDED);
      const follow = (): void => this.folded.set(width.matches);
      follow();
      width.addEventListener('change', follow);
      inject(DestroyRef).onDestroy(() => width.removeEventListener('change', follow));
    }
  }

  /** A press opens or closes the menu; Down opens it with the first item focused. */
  protected accountPressed(how: 'pointer' | 'keyboard'): void {
    if (how === 'pointer') {
      this.menuOpen.set(!this.menuOpen());
      return;
    }
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
    if (this.menuOpen() && !this.host.nativeElement.querySelector('scs-navbar .account')?.contains(target)) {
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
      this.navbar().focusAccount();
    }
  }

  private items(): HTMLButtonElement[] {
    return [...this.host.nativeElement.querySelectorAll<HTMLButtonElement>('.account-menu button')];
  }
}
