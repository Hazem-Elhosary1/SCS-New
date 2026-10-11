import { HttpClient } from '@angular/common/http';
import { DOCUMENT, Injectable, OnDestroy, effect, inject, signal, untracked } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { OnlineService } from '../network/online.service';
import type { NotificationItem } from './sources';

/** How often the console asks whether something new arrived (spec 008, research R12). */
export const NOTIFICATIONS_POLL_MS = 30_000;

interface BellState {
  readonly unread: boolean;
  readonly latestAt: string | null;
}

interface NotificationPage {
  readonly items: readonly NotificationItem[];
  readonly unread: boolean;
  readonly next: string | null;
}

const STATE = '/v1/notifications/state';
const LIST = '/v1/notifications';

/**
 * The person's own notifications, from the platform (spec 008, FR-020 to FR-028). While a
 * person is signed in, the tab is visible and there is a connection, it asks every thirty
 * seconds whether anything is unread; the list itself is fetched when the bell is opened and
 * when something new arrives while it is open. Reading is the person's own: nothing here is
 * shown to anyone else.
 */
@Injectable({ providedIn: 'root' })
export class PlatformNotifications implements OnDestroy {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly connection = inject(OnlineService);
  private readonly router = inject(Router);
  private readonly document = inject(DOCUMENT);

  private readonly loadState = signal<'ready' | 'loading' | 'failed'>('ready');
  private readonly held = signal<readonly NotificationItem[]>([]);
  private readonly dot = signal(false);
  private readonly next = signal<string | null>(null);

  private account: string | null = null;
  private timer: ReturnType<typeof setInterval> | null = null;
  private panelOpen = false;
  private latestAt: string | null = null;
  private loadingMore = false;
  private loading = false;

  /** A change of person makes every answer still on its way a stranger's: it is dropped. */
  private turn = 0;

  readonly state = this.loadState.asReadonly();
  readonly items = this.held.asReadonly();

  /** Whether the bell shows its dot. */
  readonly unread = this.dot.asReadonly();

  /** Whether older notifications can still be loaded. */
  readonly hasMore = () => this.next() !== null;

  private readonly seen = () => {
    if (this.document.visibilityState === 'visible') {
      this.ask();
    }
  };

  constructor() {
    effect(() => {
      const session = this.auth.session();
      const account =
        session === null || session.mustChangePassword === true ? null : session.accountId;
      const online = this.connection.online();
      untracked(() => {
        if (account !== this.account) {
          this.forget();
          this.account = account;
        }
        if (account !== null && online) {
          this.ask();
        }
      });
    });

    this.document.addEventListener('visibilitychange', this.seen);
    this.timer = setInterval(() => this.seen(), NOTIFICATIONS_POLL_MS);
  }

  ngOnDestroy(): void {
    this.document.removeEventListener('visibilitychange', this.seen);
    if (this.timer !== null) {
      clearInterval(this.timer);
    }
  }

  /** The bell was opened or closed. Opening it fetches the list. */
  setOpen(open: boolean): void {
    this.panelOpen = open;
    if (open) {
      this.load();
    }
  }

  retry(): void {
    this.load();
  }

  markAllRead(): void {
    if (!this.asking()) {
      return;
    }
    this.held.update((all) => all.map((item) => (item.read ? item : { ...item, read: true })));
    this.dot.set(false);
    void firstValueFrom(this.http.post(`${LIST}/read-all`, null)).catch(() => this.ask());
  }

  /** Marks it read and goes where it leads; one that leads nowhere stays where the person is. */
  open(item: NotificationItem): void {
    if (!item.read && this.asking()) {
      this.held.update((all) =>
        all.map((each) => (each.id === item.id ? { ...each, read: true } : each)),
      );
      if (!this.hasMore() && this.held().every((each) => each.read)) {
        this.dot.set(false);
      }
      void firstValueFrom(this.http.post(`${LIST}/${item.id}/read`, null)).then(
        () => this.ask(),
        () => this.ask(),
      );
    }
    if (item.link !== null && item.link !== undefined && item.link !== '') {
      void this.router.navigateByUrl('/' + item.link.replace(/^\/+/, ''));
    }
  }

  /** Older ones, after those already shown. */
  loadMore(): void {
    const before = this.next();
    if (before === null || this.loadingMore || !this.asking()) {
      return;
    }
    this.loadingMore = true;
    const turn = this.turn;
    void firstValueFrom(this.http.get<NotificationPage>(LIST, { params: { before } })).then(
      (page) => {
        this.loadingMore = false;
        if (turn !== this.turn) {
          return;
        }
        const known = new Set(this.held().map((item) => item.id));
        this.held.update((all) => [...all, ...page.items.filter((item) => !known.has(item.id))]);
        this.next.set(page.next);
        this.dot.set(page.unread);
      },
      () => {
        // The list stays as it is; scrolling to its end again asks again.
        this.loadingMore = false;
      },
    );
  }

  private asking(): boolean {
    return this.account !== null && this.connection.online();
  }

  private ask(): void {
    if (!this.asking()) {
      return;
    }
    const turn = this.turn;
    void firstValueFrom(this.http.get<BellState>(STATE)).then(
      (state) => {
        if (turn !== this.turn) {
          return;
        }
        this.dot.set(state.unread);
        const arrived = state.latestAt !== this.latestAt;
        this.latestAt = state.latestAt;
        // A list already on its way is the newest there is.
        if (arrived && this.panelOpen && !this.loading) {
          this.load();
        }
      },
      () => {
        // The bell keeps what it showed; the next turn asks again.
      },
    );
  }

  private load(): void {
    if (!this.asking()) {
      return;
    }
    if (this.held().length === 0) {
      this.loadState.set('loading');
    }
    const turn = this.turn;
    this.loading = true;
    void firstValueFrom(this.http.get<NotificationPage>(LIST)).then(
      (page) => {
        if (turn !== this.turn) {
          return;
        }
        this.loading = false;
        this.held.set(page.items);
        this.next.set(page.next);
        this.dot.set(page.unread);
        this.loadState.set('ready');
      },
      () => {
        if (turn === this.turn) {
          this.loading = false;
          this.loadState.set('failed');
        }
      },
    );
  }

  private forget(): void {
    this.turn++;
    this.held.set([]);
    this.next.set(null);
    this.dot.set(false);
    this.loadState.set('ready');
    this.latestAt = null;
    this.loadingMore = false;
    this.loading = false;
  }
}
