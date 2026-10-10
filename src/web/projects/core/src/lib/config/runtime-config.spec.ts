import { TestBed } from '@angular/core/testing';
import {
  CONFIG_FETCH,
  RuntimeConfigError,
  RuntimeConfigService,
  parseRuntimeConfig,
} from './runtime-config';

describe('parseRuntimeConfig', () => {
  it('accepts /api in every environment', () => {
    for (const environment of ['local', 'test', 'production']) {
      expect(parseRuntimeConfig({ environment, apiBaseUrl: '/api' })).toEqual({
        environment,
        apiBaseUrl: '/api',
      });
    }
  });

  it('accepts a localhost address in local only', () => {
    expect(
      parseRuntimeConfig({ environment: 'local', apiBaseUrl: 'http://localhost:5080/' }).apiBaseUrl,
    ).toBe('http://localhost:5080');

    expect(() =>
      parseRuntimeConfig({ environment: 'test', apiBaseUrl: 'http://localhost:5080' }),
    ).toThrow(RuntimeConfigError);
  });

  it('refuses a cross-origin API outside local', () => {
    expect(() =>
      parseRuntimeConfig({ environment: 'production', apiBaseUrl: 'https://api.example.org' }),
    ).toThrow(RuntimeConfigError);
  });

  it('refuses an unknown environment, a missing field and a non-object', () => {
    expect(() => parseRuntimeConfig({ environment: 'staging', apiBaseUrl: '/api' })).toThrow(
      RuntimeConfigError,
    );
    expect(() => parseRuntimeConfig({ environment: 'test' })).toThrow(RuntimeConfigError);
    expect(() => parseRuntimeConfig(null)).toThrow(RuntimeConfigError);
    expect(() => parseRuntimeConfig('nope')).toThrow(RuntimeConfigError);
  });
});

describe('RuntimeConfigService', () => {
  function service(fetchConfig: (url: string) => Promise<unknown>): RuntimeConfigService {
    TestBed.configureTestingModule({
      providers: [{ provide: CONFIG_FETCH, useValue: fetchConfig }],
    });
    return TestBed.inject(RuntimeConfigService);
  }

  it('loads config.json from the site root', async () => {
    const requested: string[] = [];
    const config = service(async (url) => {
      requested.push(url);
      return { environment: 'test', apiBaseUrl: '/api' };
    });

    await config.load();

    expect(requested).toEqual(['/config.json']);
    expect(config.value.environment).toBe('test');
  });

  it('fails the load when the file is invalid', async () => {
    const config = service(async () => ({ environment: 'test' }));

    await expect(config.load()).rejects.toBeInstanceOf(RuntimeConfigError);
  });

  it('refuses to be read before it is loaded', () => {
    const config = service(async () => ({}));

    expect(() => config.value).toThrow(RuntimeConfigError);
  });
});
