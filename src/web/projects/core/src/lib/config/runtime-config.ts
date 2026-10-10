import {
  EnvironmentProviders,
  Injectable,
  InjectionToken,
  inject,
  provideAppInitializer,
} from '@angular/core';

export type ScsEnvironment = 'local' | 'test' | 'production';

/** The contents of `config.json` (contracts/frontend-runtime.md §1). */
export interface RuntimeConfig {
  readonly environment: ScsEnvironment;
  readonly apiBaseUrl: string;
}

export class RuntimeConfigError extends Error {}

const ENVIRONMENTS: readonly string[] = ['local', 'test', 'production'];
const LOCAL_API = /^http:\/\/(localhost|127\.0\.0\.1)(:\d+)?(\/.*)?$/;

/**
 * Validates `config.json`. Outside `local` the API is always `/api` on the app's own origin;
 * an absolute `http://localhost` address is accepted in `local` only.
 */
export function parseRuntimeConfig(value: unknown): RuntimeConfig {
  if (typeof value !== 'object' || value === null) {
    throw new RuntimeConfigError('config.json is not an object');
  }

  const { environment, apiBaseUrl } = value as Record<string, unknown>;

  if (typeof environment !== 'string' || !ENVIRONMENTS.includes(environment)) {
    throw new RuntimeConfigError('config.json: environment must be local, test or production');
  }

  if (typeof apiBaseUrl !== 'string' || apiBaseUrl.length === 0) {
    throw new RuntimeConfigError('config.json: apiBaseUrl is missing');
  }

  const sameOrigin = apiBaseUrl === '/api';
  const localAddress = environment === 'local' && LOCAL_API.test(apiBaseUrl);
  if (!sameOrigin && !localAddress) {
    throw new RuntimeConfigError('config.json: apiBaseUrl must be /api outside local');
  }

  return {
    environment: environment as ScsEnvironment,
    apiBaseUrl: apiBaseUrl.replace(/\/+$/, ''),
  };
}

/** How `config.json` is fetched; replaced in tests. */
export const CONFIG_FETCH = new InjectionToken<(url: string) => Promise<unknown>>('CONFIG_FETCH', {
  providedIn: 'root',
  factory: () => async (url: string) => {
    const response = await fetch(url, { cache: 'no-store' });
    if (!response.ok) {
      throw new RuntimeConfigError(`config.json could not be loaded (${response.status})`);
    }
    return response.json();
  },
});

@Injectable({ providedIn: 'root' })
export class RuntimeConfigService {
  private readonly fetchConfig = inject(CONFIG_FETCH);
  private config: RuntimeConfig | null = null;

  /** Always loaded from the site root, so it works on any path of the patient site. */
  async load(): Promise<void> {
    this.config = parseRuntimeConfig(await this.fetchConfig('/config.json'));
  }

  get value(): RuntimeConfig {
    if (this.config === null) {
      throw new RuntimeConfigError('config.json has not been loaded');
    }
    return this.config;
  }
}

/** Loads `config.json` before the app starts; a failure stops the bootstrap. */
export function provideRuntimeConfig(): EnvironmentProviders {
  return provideAppInitializer(() => inject(RuntimeConfigService).load());
}
