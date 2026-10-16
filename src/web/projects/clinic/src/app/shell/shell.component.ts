import { CdkMenuTrigger } from '@angular/cdk/menu';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import {
  AccessService,
  AuthService,
  BranchesSource,
  ClinicTraitsSource,
  LanguageService,
  OnlineService,
  UnsavedWork,
  PreferencesService,
  ScsCanDirective,
  SubscriptionSource,
} from 'core';
import {
  ScsIconComponent,
  ScsLanguageToggleComponent,
  ScsMenuDirective,
  ScsMenuItemDirective,
  ScsToastOutletComponent,
  ToastService,
} from 'ui';
import { BellPanelComponent } from './bell-panel.component';
import { SideBarComponent } from './side-bar.component';

const BANNER_ICONS = {
  trial: 'info',
  expiring: 'clock',
  grace: 'alert-circle',
  readonly: 'lock',
} as const;

const BANNER_TONES = {
  trial: 'info',
  expiring: 'warning',
  grace: 'danger',
  readonly: 'neutral',
} as const;

/**
 * The console's frame (spec 006; design: "Shell / …" frames on page "✓ Home", Navbar,
 * Notification Panel, Subscription Banner). Every page after sign-in is shown inside it: the
 * top bar, the subscription banner when there is one, the side bar on the reading side, and
 * the page. Parts whose data belongs to a later spec read a source that says "nothing yet".
 */
@Component({
  selector: 'app-shell',
  imports: [
    RouterOutlet,
    RouterLink,
    TranslocoPipe,
    ScsCanDirective,
    CdkMenuTrigger,
    SideBarComponent,
    BellPanelComponent,
    ScsIconComponent,
    ScsLanguageToggleComponent,
    ScsMenuDirective,
    ScsMenuItemDirective,
    ScsToastOutletComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { 'data-testid': 'shell' },
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      min-inline-size: 1024px;
      block-size: calc(100dvh - var(--scs-env-strip, 0px));
      background: var(--scs-color-bg-canvas);
    }
    header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: var(--scs-spacing-md);
      flex: none;
      block-size: 64px;
      padding-inline: var(--scs-spacing-lg);
      border-block-end: 1px solid var(--scs-color-border-default);
      background: var(--scs-color-bg-surface);
    }
    .identity,
    .actions {
      display: flex;
      align-items: center;
      gap: var(--scs-spacing-12);
      min-inline-size: 0;
    }
    .logo {
      display: grid;
      place-items: center;
      flex: none;
      inline-size: 36px;
      block-size: 36px;
      border-radius: var(--scs-radius-md);
      background: var(--scs-color-brand-default);
      color: var(--scs-color-brand-on-brand);
      font-size: var(--scs-text-heading-sm);
      font-weight: 700;
    }
    .clinic {
      max-inline-size: 320px;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
      font-size: var(--scs-text-heading-sm);
      font-weight: 500;
      color: var(--scs-color-text-primary);
    }
    .divider {
      flex: none;
      inline-size: 1px;
      block-size: 24px;
      background: var(--scs-color-border-default);
    }
    .plain {
      display: inline-flex;
      align-items: center;
      gap: var(--scs-spacing-sm);
      border: 0;
      border-radius: var(--scs-radius-md);
      background: none;
      color: var(--scs-color-text-primary);
      font: inherit;
      cursor: pointer;
    }
    .plain:hover,
    .plain[aria-expanded='true'] {
      background: var(--scs-color-bg-surface-secondary);
    }
    .plain:focus-visible {
      outline: 2px solid var(--scs-color-brand-default);
      outline-offset: 1px;
    }
    .branch {
      block-size: 32px;
      padding-inline: var(--scs-spacing-sm);
      font-size: var(--scs-text-body-sm);
      font-weight: 500;
    }
    .chip {
      gap: var(--scs-spacing-10);
      block-size: 54px;
      padding-inline: var(--scs-spacing-sm);
      text-align: start;
    }
    .mark {
      display: grid;
      place-items: center;
      flex: none;
      inline-size: 32px;
      block-size: 32px;
      border-radius: var(--scs-radius-full);
      background: var(--scs-color-brand-subtle);
      color: var(--scs-color-brand-active);
      font-size: var(--scs-text-body-sm);
      font-weight: 600;
    }
    .who {
      display: flex;
      flex-direction: column;
      min-inline-size: 0;
      max-inline-size: 200px;
    }
    .who span {
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .name {
      font-size: var(--scs-text-body-sm);
      font-weight: 500;
    }
    .role {
      font-size: var(--scs-text-label);
      color: var(--scs-color-text-tertiary);
    }
    /* Design: subscription-banner — 48 high, 24 at the sides; the state at the reading start,
       its action and the × at the far end. */
    .subscription {
      display: flex;
      align-items: center;
      gap: var(--scs-spacing-12);
      flex: none;
      min-block-size: 48px;
      padding-inline: var(--scs-spacing-lg);
      font-size: var(--scs-text-body-sm);
      font-weight: 500;
    }
    .subscription .words {
      flex: 1;
      min-inline-size: 0;
    }
    .subscription[data-tone='info'] {
      background: var(--scs-color-feedback-info-bg);
      color: var(--scs-color-feedback-info-text);
    }
    .subscription[data-tone='info'] scs-icon {
      color: var(--scs-color-feedback-info-icon);
    }
    .subscription[data-tone='warning'] {
      background: var(--scs-color-feedback-warning-bg);
      color: var(--scs-color-feedback-warning-text);
    }
    .subscription[data-tone='warning'] scs-icon {
      color: var(--scs-color-feedback-warning-icon);
    }
    .subscription[data-tone='danger'] {
      background: var(--scs-color-feedback-danger-bg);
      color: var(--scs-color-feedback-danger-text);
    }
    .subscription[data-tone='danger'] scs-icon {
      color: var(--scs-color-feedback-danger-icon);
    }
    .subscription[data-tone='neutral'] {
      background: var(--scs-color-bg-surface-secondary);
      color: var(--scs-color-text-secondary);
    }
    .subscription[data-tone='neutral'] scs-icon {
      color: var(--scs-color-icon-default);
    }
    .subscription a {
      color: inherit;
      font-weight: 600;
      text-decoration: none;
    }
    .subscription a:hover {
      text-decoration: underline;
    }
    .subscription button {
      display: inline-grid;
      place-items: center;
      padding: 0;
      border: 0;
      background: none;
      color: inherit;
      cursor: pointer;
    }
    .subscription a:focus-visible,
    .subscription button:focus-visible {
      outline: 2px solid currentColor;
      outline-offset: 2px;
    }
    .body {
      position: relative;
      display: flex;
      flex: 1;
      min-block-size: 0;
    }
    /* The passing notice: the far corner of the content, the header buttons moving aside for it. */
    scs-toast-outlet {
      position: absolute;
      inset-block-start: var(--scs-spacing-md);
    }
    main {
      flex: 1;
      min-inline-size: 0;
      padding: var(--scs-spacing-lg) var(--scs-spacing-xl);
      overflow: auto;
    }
  `,
  template: `
    <header data-testid="top-bar">
      <div class="identity">
        <span class="logo" lang="ar" aria-hidden="true">{{ mark }}</span>
        <span class="clinic" data-testid="signed-in-clinic" [attr.title]="clinicName()">{{
          clinicName()
        }}</span>
        @if (branches.multiBranch() && branches.branches().length > 0) {
          <span class="divider"></span>
          <button
            type="button"
            class="plain branch"
            data-testid="branch-switch"
            [cdkMenuTriggerFor]="severalBranches() ? branchMenu : null"
            [cdkMenuPosition]="nearAligned"
          >
            <scs-icon [name]="branches.choice() === 'all' ? 'layers' : 'building'" [size]="16" />
            <span>{{ branchLabel() ?? ('app.shell.allBranches' | transloco) }}</span>
            @if (severalBranches()) {
              <scs-icon name="chevron-down" [size]="16" />
            }
          </button>
        }
      </div>

      <div class="actions">
        <scs-language-toggle
          [current]="language.language()"
          [label]="'ui.language.switch.label' | transloco"
          (chosen)="preferences.setLanguage($event)"
        />
        <app-bell-panel />
        <span class="divider"></span>
        <button
          type="button"
          class="plain chip"
          data-testid="account-chip"
          [cdkMenuTriggerFor]="accountMenu"
          [cdkMenuPosition]="farAligned"
        >
          <span class="mark" aria-hidden="true">{{ initial() }}</span>
          <span class="who">
            <span class="name" data-testid="signed-in-email" [attr.title]="email()"
              ><bdi>{{ email() }}</bdi></span
            >
            @if (roleKey(); as key) {
              <span class="role" data-testid="account-role">{{ key | transloco }}</span>
            } @else if (roleName()) {
              <span class="role" data-testid="account-role" [attr.title]="roleName()">{{
                roleName()
              }}</span>
            }
          </span>
          <scs-icon name="chevron-down" [size]="16" />
        </button>
      </div>
    </header>

    <ng-template #accountMenu>
      <div scsMenu data-testid="account-menu">
        <a scsMenuItem routerLink="/account/subscription" data-testid="menu-subscription">
          <scs-icon name="user" [size]="18" />
          {{ 'app.nav.subscription' | transloco }}
        </a>
        @if (traits.linkedToDoctor()) {
          <a scsMenuItem routerLink="/account/doctor-profile" data-testid="menu-doctor-profile">
            <scs-icon name="stethoscope" [size]="18" />
            {{ 'app.nav.doctorProfile' | transloco }}
          </a>
        }
        <div class="scs-menu-separator" role="separator"></div>
        <button type="button" scsMenuItem data-testid="sign-out" (chosen)="signOut()">
          <scs-icon name="sign-out" [size]="18" />
          {{ 'app.shell.signOut' | transloco }}
        </button>
      </div>
    </ng-template>

    <ng-template #branchMenu>
      <div scsMenu data-testid="branch-menu">
        <button
          type="button"
          scsMenuItem
          [active]="branches.choice() === 'all'"
          (chosen)="branches.choose('all')"
        >
          <scs-icon name="layers" [size]="18" />
          {{ 'app.shell.allBranches' | transloco }}
          @if (branches.choice() === 'all') {
            <scs-icon class="end" name="check" [size]="16" />
          }
        </button>
        @for (branch of branches.branches(); track branch.id) {
          <button
            type="button"
            scsMenuItem
            [active]="branches.choice() === branch.id"
            (chosen)="branches.choose(branch.id)"
          >
            <scs-icon name="building" [size]="18" />
            {{ language.language() === 'ar' ? branch.nameAr : branch.nameEn }}
            @if (branches.choice() === branch.id) {
              <scs-icon class="end" name="check" [size]="16" />
            }
          </button>
        }
      </div>
    </ng-template>

    @if (banner(); as notice) {
      <div
        class="subscription"
        role="status"
        data-testid="subscription-banner"
        [attr.data-tone]="tone()"
        [attr.data-state]="notice.state"
      >
        <scs-icon [name]="bannerIcons[notice.state]" [size]="18" />
        <span class="words">{{
          'app.shell.subscription.' + notice.state | transloco: { days: days() }
        }}</span>
        <a
          *scsCan="'subscription.view'"
          routerLink="/account/subscription"
          data-testid="subscription-action"
          >{{
            (notice.state === 'trial'
              ? 'app.shell.subscription.choosePlan'
              : 'app.shell.subscription.renew'
            ) | transloco
          }}</a
        >
        @if (notice.state !== 'readonly') {
          <button
            type="button"
            data-testid="subscription-close"
            [attr.aria-label]="'ui.dialog.close' | transloco"
            (click)="closeBanner()"
          >
            <scs-icon name="close" [size]="16" />
          </button>
        }
      </div>
    }

    <div class="body" data-testid="signed-in">
      <app-side-bar />
      <main [style.--scs-toast-room]="toastRoom()">
        @for (reading of [reading()]; track reading) {
          <router-outlet />
        }
      </main>
      <scs-toast-outlet [closeLabel]="'ui.dialog.close' | transloco" />
    </div>
  `,
})
export class ShellComponent {
  private readonly auth = inject(AuthService);
  private readonly access = inject(AccessService);
  private readonly subscription = inject(SubscriptionSource);

  protected readonly language = inject(LanguageService);
  protected readonly preferences = inject(PreferencesService);
  protected readonly online = inject(OnlineService);
  private readonly unsaved = inject(UnsavedWork);
  protected readonly branches = inject(BranchesSource);
  protected readonly traits = inject(ClinicTraitsSource);
  protected readonly toasts = inject(ToastService);

  protected readonly bannerIcons = BANNER_ICONS;

  /** The product's mark: the letter the Arabic word for "clinic" begins with. */
  protected readonly mark = 'ع';

  protected readonly severalBranches = computed(() => this.branches.branches().length > 1);

  /** Room the header buttons leave for a passing notice while one is shown. */
  protected readonly toastRoom = computed(() => (this.toasts.toasts().length > 0 ? '376px' : null));

  /** Counts the times the page was read again; a new number draws the page anew. */
  protected readonly reading = signal(0);
  private wasOffline = false;

  constructor() {
    // When the connection returns, what the person may do and the page itself are read again
    // (FR-021): what was on screen is the last copy loaded before the connection went. A page
    // that holds work not yet saved — a chosen file — is left as it is: reading it again
    // would lose that work (spec 009, US4).
    effect(() => {
      if (!this.online.online()) {
        this.wasOffline = true;
      } else if (this.wasOffline) {
        this.wasOffline = false;
        void this.auth.refreshSession().then(() => {
          if (!this.unsaved.any()) {
            this.reading.update((times) => times + 1);
          }
        });
      }
    });
  }
  /** Under the opener, aligned to its far edge. */
  protected readonly farAligned = [
    { originX: 'end', originY: 'bottom', overlayX: 'end', overlayY: 'top', offsetY: 5 } as const,
  ];
  protected readonly nearAligned = [
    {
      originX: 'start',
      originY: 'bottom',
      overlayX: 'start',
      overlayY: 'top',
      offsetY: 16,
    } as const,
  ];

  protected readonly clinicName = computed(() => {
    const clinic = this.auth.session()?.clinic;
    return clinic === undefined
      ? ''
      : this.language.language() === 'ar'
        ? clinic.nameAr
        : clinic.nameEn;
  });

  /** Until accounts have a name (spec 026) the chip shows the email where the name goes. */
  protected readonly email = computed(() => this.auth.session()?.email ?? '');
  protected readonly initial = computed(() => this.email().charAt(0).toUpperCase());
  protected readonly roleKey = this.access.roleNameKey;
  protected readonly roleName = computed(() => this.access.role()?.name ?? '');

  protected readonly branchLabel = computed(() => {
    const choice = this.branches.choice();
    const all = this.branches.branches();
    const one = all.length === 1 ? all[0] : all.find((branch) => branch.id === choice);
    if (one === undefined) {
      return null;
    }
    return this.language.language() === 'ar' ? one.nameAr : one.nameEn;
  });

  /** The subscription banner, unless the person closed this state today. */
  protected readonly banner = computed(() => {
    const notice = this.subscription.notice();
    if (notice === null) {
      return null;
    }
    return notice.state !== 'readonly' && this.preferences.dismissedBanner() === notice.state
      ? null
      : notice;
  });

  protected readonly tone = computed(() => BANNER_TONES[this.banner()?.state ?? 'readonly']);

  /** The days left, in the digits of the page's language. */
  protected readonly days = computed(() =>
    new Intl.NumberFormat(this.language.language() === 'ar' ? 'ar-EG-u-nu-arab' : 'en-GB').format(
      this.banner()?.days ?? 0,
    ),
  );

  protected closeBanner(): void {
    const state = this.banner()?.state;
    if (state !== undefined && state !== 'readonly') {
      this.preferences.dismissBanner(state);
    }
  }

  protected async signOut(): Promise<void> {
    await this.auth.signOut();
  }
}
