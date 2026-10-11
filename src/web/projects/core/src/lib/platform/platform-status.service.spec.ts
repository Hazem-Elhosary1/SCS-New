import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { CONFIG_FETCH, RuntimeConfigService } from '../config/runtime-config';
import { apiInterceptor } from '../http/api.interceptor';
import { PlatformStatusService } from './platform-status.service';

const VERSION = '/api/v1/meta/version';
const READY = '/api/health/ready';

const release = {
  version: '0.1.0+3f9c2ab',
  commit: '3f9c2ab4d5e6f708192a3b4c5d6e7f8091a2b3c4',
  builtAt: '2026-10-10T09:30:00Z',
  environment: 'test',
};

describe('PlatformStatusService', () => {
  let status: PlatformStatusService;
  let backend: HttpTestingController;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([apiInterceptor])),
        provideHttpClientTesting(),
        {
          provide: CONFIG_FETCH,
          useValue: async () => ({ environment: 'test', apiBaseUrl: '/api' }),
        },
      ],
    });
    await TestBed.inject(RuntimeConfigService).load();
    status = TestBed.inject(PlatformStatusService);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  it('starts by checking', () => {
    expect(status.status().state).toBe('checking');
  });

  it('reports reachable with the version and environment when the platform is ready', async () => {
    const check = status.check();
    backend.expectOne(VERSION).flush(release);
    backend.expectOne(READY).flush({ status: 'Healthy', checks: [] });

    const result = await check;

    expect(result.state).toBe('reachable');
    expect(result.state === 'reachable' && result.platform.version).toBe('0.1.0+3f9c2ab');
    expect(result.state === 'reachable' && result.platform.environment).toBe('test');
    expect(status.status()).toEqual(result);
  });

  it('reports unreachable with a reference when the platform is alive but not ready', async () => {
    const check = status.check();
    backend.expectOne(VERSION).flush(release);
    backend
      .expectOne(READY)
      .flush(
        { status: 'Unhealthy', checks: [{ name: 'database', status: 'Unhealthy' }] },
        { status: 503, statusText: 'Service Unavailable' },
      );

    const result = await check;

    expect(result.state).toBe('unreachable');
    expect(result.state === 'unreachable' && result.problem.code).toBe('SERVICE_UNAVAILABLE');
    expect(result.state === 'unreachable' && result.problem.traceId).toMatch(/^[0-9a-f]{32}$/);
  });

  it('reports unreachable with a reference when the platform does not answer', async () => {
    const check = status.check();
    backend.expectOne(VERSION).flush('gateway timeout', { status: 504, statusText: 'Timeout' });
    backend.expectOne(READY).flush('gateway timeout', { status: 504, statusText: 'Timeout' });

    const result = await check;

    expect(result.state).toBe('unreachable');
    expect(result.state === 'unreachable' && result.problem.code).toBe('SERVICE_UNAVAILABLE');
    expect(result.state === 'unreachable' && result.problem.traceId).toMatch(/^[0-9a-f]{32}$/);
  });

  it('carries the platform reference when the platform answers with a failure', async () => {
    const check = status.check();
    backend.expectOne(VERSION).flush(
      {
        type: 'urn:scs:error:service-unavailable',
        title: 'الخدمة غير متاحة مؤقتًا',
        status: 503,
        code: 'SERVICE_UNAVAILABLE',
        traceId: '4bf92f3577b34da6a3ce929d0e0e4736',
      },
      { status: 503, statusText: 'Service Unavailable' },
    );
    backend.expectOne(READY).flush({ status: 'Healthy' });

    const result = await check;

    expect(result.state === 'unreachable' && result.problem.traceId).toBe(
      '4bf92f3577b34da6a3ce929d0e0e4736',
    );
  });
});
