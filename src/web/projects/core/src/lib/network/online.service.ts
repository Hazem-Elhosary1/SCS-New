import { DOCUMENT, Injectable, inject, signal } from '@angular/core';

/**
 * Whether the browser has a connection. Screens show the `offline` state while it is false,
 * and work resumes by itself when it returns (contracts/frontend-runtime.md §5).
 */
@Injectable({ providedIn: 'root' })
export class OnlineService {
  private readonly window = inject(DOCUMENT).defaultView;
  private readonly state = signal(this.window?.navigator.onLine ?? true);

  readonly online = this.state.asReadonly();

  constructor() {
    this.window?.addEventListener('online', () => this.state.set(true));
    this.window?.addEventListener('offline', () => this.state.set(false));
  }
}
