import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ScsProblem, ScsProblemError } from '../http/problem';

/** Answer of `GET /v1/meta/version` (contracts/platform-api.openapi.yaml). */
export interface PlatformVersion {
  readonly version: string;
  readonly commit: string;
  readonly builtAt: string;
  readonly environment: string;
}

export type PlatformStatus =
  | { readonly state: 'checking' }
  | { readonly state: 'reachable'; readonly platform: PlatformVersion }
  | { readonly state: 'unreachable'; readonly problem: ScsProblem };

/**
 * Tells a front end whether the platform can serve it, and which release answered. The
 * platform counts as reachable only when it is also ready: a platform that is alive but cannot
 * reach its database is reported as unreachable, with a reference.
 */
@Injectable({ providedIn: 'root' })
export class PlatformStatusService {
  private readonly http = inject(HttpClient);
  private readonly current = signal<PlatformStatus>({ state: 'checking' });

  readonly status = this.current.asReadonly();

  async check(): Promise<PlatformStatus> {
    this.current.set({ state: 'checking' });
    try {
      const [platform] = await Promise.all([
        firstValueFrom(this.http.get<PlatformVersion>('/v1/meta/version')),
        firstValueFrom(this.http.get('/health/ready')),
      ]);
      this.current.set({ state: 'reachable', platform });
    } catch (error) {
      if (!(error instanceof ScsProblemError)) {
        throw error;
      }
      this.current.set({ state: 'unreachable', problem: error.problem });
    }
    return this.current();
  }
}
