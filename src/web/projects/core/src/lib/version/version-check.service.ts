import { Injectable, InjectionToken, inject, signal } from '@angular/core';
import { NavigationStart, Router } from '@angular/router';

/** The contents of `version.json` (contracts/frontend-runtime.md §2). */
export interface AppVersion {
  readonly app: string;
  readonly version: string;
  readonly commit: string;
  readonly builtAt: string;
}

export const VERSION_CHECK_INTERVAL_MS = 10 * 60 * 1000;

/** How `version.json` is fetched; replaced in tests. */
export const VERSION_FETCH = new InjectionToken<() => Promise<unknown>>('VERSION_FETCH', {
  providedIn: 'root',
  factory: () => async () => {
    const response = await fetch('/version.json', { cache: 'no-store' });
    if (!response.ok) {
      throw new Error(`version.json could not be loaded (${response.status})`);
    }
    return response.json();
  },
});

/** How the page is reloaded onto the new release; replaced in tests. */
export const VERSION_RELOAD = new InjectionToken<(url: string) => void>('VERSION_RELOAD', {
  providedIn: 'root',
  factory: () => (url: string) => window.location.assign(url),
});

function isAppVersion(value: unknown): value is AppVersion {
  return (
    typeof value === 'object' &&
    value !== null &&
    typeof (value as Record<string, unknown>)['commit'] === 'string' &&
    typeof (value as Record<string, unknown>)['version'] === 'string'
  );
}

/**
 * Notices when a newer release has been deployed while the app is open, and moves the user to
 * it at the next navigation, never in the middle of what they are doing (FR-011).
 */
@Injectable({ providedIn: 'root' })
export class VersionCheckService {
  private readonly fetchVersion = inject(VERSION_FETCH);
  private readonly reload = inject(VERSION_RELOAD);
  private readonly router = inject(Router);

  private readonly running = signal<AppVersion | null>(null);
  private readonly update = signal(false);
  private timer: ReturnType<typeof setInterval> | null = null;

  /** The release this tab started with; `null` until the first read succeeds. */
  readonly current = this.running.asReadonly();

  /** True once a different release has been seen. */
  readonly updateAvailable = this.update.asReadonly();

  constructor() {
    this.router.events.subscribe((event) => {
      if (event instanceof NavigationStart && this.update()) {
        this.reload(event.url);
      }
    });
  }

  /** Reads the running release, then re-reads it on a timer. */
  async start(intervalMs: number = VERSION_CHECK_INTERVAL_MS): Promise<void> {
    await this.check();
    this.timer ??= setInterval(() => void this.check(), intervalMs);
  }

  stop(): void {
    if (this.timer !== null) {
      clearInterval(this.timer);
      this.timer = null;
    }
  }

  async check(): Promise<void> {
    let seen: unknown;
    try {
      seen = await this.fetchVersion();
    } catch {
      // Offline, or a development server with no version.json: nothing to compare.
      return;
    }

    if (!isAppVersion(seen)) {
      return;
    }

    const started = this.running();
    if (started === null) {
      this.running.set(seen);
    } else if (started.commit !== seen.commit) {
      this.update.set(true);
    }
  }
}
