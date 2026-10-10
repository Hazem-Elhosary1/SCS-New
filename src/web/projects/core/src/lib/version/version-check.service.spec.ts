import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { VERSION_FETCH, VERSION_RELOAD, VersionCheckService } from './version-check.service';

@Component({ template: '' })
class BlankPage {}

const release = (commit: string) => ({
  app: 'clinic',
  version: `0.1.0+${commit.slice(0, 7)}`,
  commit,
  builtAt: '2026-10-10T09:30:00Z',
});

const FIRST = 'a'.repeat(40);
const SECOND = 'b'.repeat(40);

describe('VersionCheckService', () => {
  let deployed: unknown;
  let reloads: string[];
  let service: VersionCheckService;
  let router: Router;

  beforeEach(() => {
    deployed = release(FIRST);
    reloads = [];

    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: '**', component: BlankPage }]),
        { provide: VERSION_FETCH, useValue: async () => deployed },
        { provide: VERSION_RELOAD, useValue: (url: string) => reloads.push(url) },
      ],
    });
    service = TestBed.inject(VersionCheckService);
    router = TestBed.inject(Router);
  });

  afterEach(() => service.stop());

  it('reads the running release at start', async () => {
    await service.check();

    expect(service.current()?.commit).toBe(FIRST);
    expect(service.updateAvailable()).toBe(false);
  });

  it('flags an update when a later read shows a different commit, without reloading', async () => {
    await service.check();
    deployed = release(SECOND);

    await service.check();

    expect(service.updateAvailable()).toBe(true);
    expect(service.current()?.commit).toBe(FIRST);
    expect(reloads).toEqual([]);
  });

  it('reloads at the next navigation, onto the address being opened', async () => {
    await service.check();
    deployed = release(SECOND);
    await service.check();

    await router.navigateByUrl('/patients/7');

    expect(reloads).toEqual(['/patients/7']);
  });

  it('does not reload on navigation while the release is unchanged', async () => {
    await service.check();
    await service.check();

    await router.navigateByUrl('/patients/7');

    expect(reloads).toEqual([]);
  });

  it('ignores a read that fails or is not a version', async () => {
    await service.check();

    deployed = '<html>not json</html>';
    await service.check();
    expect(service.updateAvailable()).toBe(false);

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: VERSION_FETCH,
          useValue: async () => {
            throw new Error('offline');
          },
        },
      ],
    });
    const offline = TestBed.inject(VersionCheckService);

    await expect(offline.check()).resolves.toBeUndefined();
    expect(offline.current()).toBeNull();
  });

  it('re-reads on a timer once started', async () => {
    await service.start(5);
    deployed = release(SECOND);

    await new Promise((resolve) => setTimeout(resolve, 40));

    expect(service.updateAvailable()).toBe(true);
  });
});
