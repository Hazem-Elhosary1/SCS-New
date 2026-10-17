import { Injectable, Signal, effect, inject, signal, untracked } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { AuthService, StaffPreferences } from '../auth/auth.service';
import { LanguageService, ScsLanguage } from '../i18n/language.service';
import { PlatformNotifications } from './platform-notifications';

/**
 * The parts of the console's frame that wait for a later spec's data (spec 006, "frame now,
 * data later"). Each is a source with a default that says "nothing yet": the bell has no
 * notifications, the clinic has one branch, there is no subscription banner, every destination
 * is in the plan. The spec that owns the data provides its own implementation of the source —
 * `{ provide: NotificationsSource, useClass: … }` — and nothing in the frame changes. The bell
 * has its data since spec 008: its source is the platform's, `PlatformNotifications`.
 */

export type NotificationKind =
  | 'ticket'
  | 'invoice'
  | 'subscription'
  | 'feedback'
  | 'notice'
  | 'warning'
  | 'incident';

export interface NotificationItem {
  readonly id: string;
  readonly kind: NotificationKind;
  readonly title: string;
  readonly text: string;
  /** An instant, as the platform sends it. */
  readonly at: string;
  readonly read: boolean;
  /** Where pressing it leads, a path inside the console; none when it leads nowhere. */
  readonly link?: string | null;
}

/**
 * What the bell shows: the person's own notifications from the platform (spec 008). The class
 * itself is the source that has nothing, for a place that wants a bell with no platform.
 */
@Injectable({ providedIn: 'root', useFactory: () => inject(PlatformNotifications) })
export class NotificationsSource {
  readonly state: Signal<'ready' | 'loading' | 'failed'> = signal('ready');
  readonly items: Signal<readonly NotificationItem[]> = signal([]);

  /** Whether the bell shows its dot: something is unread, loaded or not. */
  readonly unread: Signal<boolean> = signal(false);

  /** Whether older notifications can still be loaded. */
  hasMore(): boolean {
    return false;
  }

  /** The bell was opened or closed. */
  setOpen(open: boolean): void {
    void open;
  }

  markAllRead(): void {
    // Nothing to mark.
  }

  open(item: NotificationItem): void {
    void item;
    // Nothing to open.
  }

  retry(): void {
    // Nothing to load.
  }

  loadMore(): void {
    // Nothing older.
  }
}

export interface BranchChoice {
  readonly id: string;
  readonly nameAr: string;
  readonly nameEn: string;
}

/** The person's branches and which one they are looking at. Filled by the branches spec (028). */
@Injectable({ providedIn: 'root' })
export class BranchesSource {
  /** The branches given to the person. Fewer than two: the clinic has one branch for them. */
  readonly branches: Signal<readonly BranchChoice[]> = signal([]);

  /** `all`, or the id of one branch. */
  readonly choice: Signal<string> = signal('all');

  /** Whether the clinic has more than one branch at all; with one, the switch is absent. */
  readonly multiBranch: Signal<boolean> = signal(false);

  choose(choice: string): void {
    void choice;
    // One branch: nothing to choose.
  }
}

export type SubscriptionBannerState = 'trial' | 'expiring' | 'grace' | 'readonly';

export interface SubscriptionNotice {
  readonly state: SubscriptionBannerState;
  /** Days left; not used by `readonly`. */
  readonly days: number;
}

/** The subscription banner's state. Filled by the subscription specs (012 to 016). */
@Injectable({ providedIn: 'root' })
export class SubscriptionSource {
  readonly notice: Signal<SubscriptionNotice | null> = signal(null);
}

/**
 * Which destinations the clinic's plan leaves out. Each console gives its own (the clinic's
 * reads the session's plan, spec 014); this one leaves nothing out.
 */
@Injectable({ providedIn: 'root' })
export class PlanSource {
  excludes(destinationKey: string): boolean {
    void destinationKey;
    return false;
  }

  /** The name of what the plan leaves out on that destination, when no words are drawn for it. */
  featureName(destinationKey: string, language: string): string | null {
    void destinationKey;
    void language;
    return null;
  }

  /** Reads the plan again before a destination decides (spec 014, FR-029). */
  refresh(): Promise<void> {
    return Promise.resolve();
  }
}

/** The unread count beside «الصندوق الوارد». Filled by the inbox's spec. */
@Injectable({ providedIn: 'root' })
export class InboxCountSource {
  readonly unread: Signal<number | null> = signal(null);
}

/** Facts about the clinic and the account that change what the frame offers. */
@Injectable({ providedIn: 'root' })
export class ClinicTraitsSource {
  /** A dental clinic sees «معمل الأسنان» (SH-012). Filled by clinic creation (015). */
  readonly dental: Signal<boolean> = signal(false);

  /** An account linked to a doctor's file sees «ملف الطبيب». Filled by the doctors spec (029). */
  readonly linkedToDoctor: Signal<boolean> = signal(false);
}

const NOTHING: StaffPreferences = { language: null, sidebarFolded: null, dismissedBanner: null };

/**
 * What the signed-in person keeps between sessions and across browsers (spec 006, FR-017): the
 * language of the console, whether the side bar is folded, and a subscription banner closed
 * for today. A choice applies at once and is then saved; a save that fails is kept and sent
 * with the next one, and the person is never shown an error for it.
 */
@Injectable({ providedIn: 'root' })
export class PreferencesService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly language = inject(LanguageService);

  private readonly kept = signal<StaffPreferences>(NOTHING);
  private unsent: Partial<Record<keyof StaffPreferences, unknown>> = {};
  private adoptedFor: string | null = null;

  /** Null while the person never chose: the width of the screen then decides. */
  readonly sidebarFolded = () => this.kept().sidebarFolded;

  /** The banner state the person closed today, or null. */
  readonly dismissedBanner = () => this.kept().dismissedBanner;

  constructor() {
    // When a person's session arrives, take what they kept; a saved language wins over the
    // browser's, and with none saved the browser's becomes their first choice (FR-019).
    effect(() => {
      const session = this.auth.session();
      if (session === null) {
        this.adoptedFor = null;
        this.unsent = {};
        untracked(() => this.kept.set(NOTHING));
        return;
      }

      const saved = session.preferences ?? NOTHING;
      untracked(() => {
        this.kept.set({ ...saved, ...this.pendingLocal() });
        if (this.adoptedFor === session.accountId || session.mustChangePassword === true) {
          return;
        }
        this.adoptedFor = session.accountId;
        if (saved.language === 'ar' || saved.language === 'en') {
          this.language.use(saved.language);
        } else {
          this.save({ language: this.language.language() });
        }
      });
    });
  }

  /** Turns the console to a language at once, and keeps the choice for the person. */
  setLanguage(language: ScsLanguage): void {
    this.language.use(language);
    this.kept.update((kept) => ({ ...kept, language }));
    this.save({ language });
  }

  setSidebarFolded(sidebarFolded: boolean): void {
    this.kept.update((kept) => ({ ...kept, sidebarFolded }));
    this.save({ sidebarFolded });
  }

  /** Closes a subscription banner for the person until tomorrow. */
  dismissBanner(state: 'trial' | 'expiring' | 'grace'): void {
    this.kept.update((kept) => ({ ...kept, dismissedBanner: state }));
    this.save({ dismissedBanner: state });
  }

  /** What was chosen on this screen and has not reached the platform yet. */
  private pendingLocal(): Partial<StaffPreferences> {
    return this.unsent as Partial<StaffPreferences>;
  }

  private save(change: Partial<Record<keyof StaffPreferences, unknown>>): void {
    const body = { ...this.unsent, ...change };
    this.unsent = body;
    void firstValueFrom(this.http.put('/v1/account/preferences', body)).then(
      () => {
        if (this.unsent === body) {
          this.unsent = {};
        }
      },
      () => {
        // Kept in `unsent`: it goes with the next choice. Nothing is shown.
      },
    );
  }
}
