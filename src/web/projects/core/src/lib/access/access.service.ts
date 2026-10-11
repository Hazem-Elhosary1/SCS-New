import {
  Directive,
  Injectable,
  OnDestroy,
  TemplateRef,
  ViewContainerRef,
  computed,
  effect,
  inject,
  input,
} from '@angular/core';
import { CanMatchFn } from '@angular/router';
import { AuthService } from '../auth/auth.service';
import { LanguageService } from '../i18n/language.service';
import catalogue from './permission-catalogue.json';

/** One line of the platform's permission catalogue (BRD Volume 1, Chapter 10 §2-3). */
export interface Permission {
  readonly number: number;
  readonly code: string;
  readonly nameAr: string;
  readonly nameEn: string;
  readonly group: string;
  readonly kind: 'view' | 'read' | 'write' | 'scope';
  readonly requires: string | null;
}

export interface PermissionGroup {
  readonly key: string;
  readonly nameAr: string;
  readonly nameEn: string;
}

/**
 * The catalogue as the platform holds it: generated from the server's own list, never edited
 * by hand (`Scs.Migrator catalogue`). A server test fails while this file is out of date.
 */
export const PERMISSIONS = catalogue.permissions as readonly Permission[];
export const PERMISSION_GROUPS = catalogue.groups as readonly PermissionGroup[];

const BY_CODE = new Map(PERMISSIONS.map((permission) => [permission.code, permission]));

/** How often an open page asks again what its person may do; the spec allows fifteen minutes. */
export const ACCESS_REFRESH_MS = 10 * 60 * 1000;

/** The ready roles a clinic starts with; shown in the page's language until a clinic renames one. */
const READY_ROLE_KEYS: Record<string, string> = {
  owner: 'ui.access.role.owner',
  manager: 'ui.access.role.manager',
  doctor: 'ui.access.role.doctor',
};

/**
 * What the signed-in person may do (spec 005). The platform decides on every request; this only
 * lets a screen leave out what the person may not use, and say which permission a page needs.
 * It is told at sign-in, asks again every ten minutes while a page is open, and asks at once
 * when the platform refuses for a missing permission — so a stale list corrects itself.
 */
@Injectable({ providedIn: 'root' })
export class AccessService implements OnDestroy {
  private readonly auth = inject(AuthService);
  private readonly language = inject(LanguageService);

  private readonly held = computed(() => new Set(this.auth.session()?.permissions ?? []));
  private timer: ReturnType<typeof setInterval> | null = null;

  /** The codes the person holds, as the platform last said. */
  readonly permissions = computed(() => this.auth.session()?.permissions ?? []);

  /** The role the person holds, or null when their account holds none. */
  readonly role = computed(() => this.auth.session()?.role ?? null);

  /**
   * The translation key of a ready role that still has its original name, or null: the page
   * then shows the name as the clinic wrote it.
   */
  readonly roleNameKey = computed(() => {
    const key = this.role()?.readyKey;
    return key ? (READY_ROLE_KEYS[key] ?? null) : null;
  });

  constructor() {
    // Ask again while somebody is signed in, and stop when nobody is.
    effect(() => {
      const signedIn = this.auth.status() === 'signed-in';
      if (signedIn && this.timer === null) {
        this.timer = setInterval(() => void this.auth.refreshSession(), ACCESS_REFRESH_MS);
      } else if (!signedIn) {
        this.stop();
      }
    });
  }

  ngOnDestroy(): void {
    this.stop();
  }

  holds(code: string): boolean {
    return this.held().has(code);
  }

  /** The permission's name in the page's language; the code itself for one the catalogue lacks. */
  nameOf(code: string): string {
    const permission = BY_CODE.get(code);
    if (permission === undefined) {
      return code;
    }
    return this.language.language() === 'ar' ? permission.nameAr : permission.nameEn;
  }


  private stop(): void {
    if (this.timer !== null) {
      clearInterval(this.timer);
      this.timer = null;
    }
  }
}

/**
 * Lets a route match only for a holder of the permission. Put the destination first with this
 * guard and the "not in your role" page second on the same path: a person without the
 * permission never loads the destination and no request for its content is made (FR-029).
 */
export function holds(code: string): CanMatchFn {
  return async () => {
    const auth = inject(AuthService);
    const access = inject(AccessService);
    await auth.load();
    return access.holds(code);
  };
}

/**
 * Renders its content only for a holder of the permission. A control the person may not use is
 * absent from the page — not disabled (ق-37). Disabling is for other reasons: the state of a
 * record, the plan, the subscription.
 *
 * ```html
 * <button *scsCan="'admin.roles.manage'" scsButton>…</button>
 * ```
 */
@Directive({ selector: '[scsCan]' })
export class ScsCanDirective {
  private readonly template = inject<TemplateRef<unknown>>(TemplateRef);
  private readonly container = inject(ViewContainerRef);
  private readonly access = inject(AccessService);

  readonly scsCan = input.required<string>();

  constructor() {
    effect(() => {
      const allowed = this.access.holds(this.scsCan());
      if (allowed && this.container.length === 0) {
        this.container.createEmbeddedView(this.template);
      } else if (!allowed) {
        this.container.clear();
      }
    });
  }
}
