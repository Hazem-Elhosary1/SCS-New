import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { DOCUMENT } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { CONFIG_FETCH, RuntimeConfigService } from '../config/runtime-config';
import { apiInterceptor } from './api.interceptor';
import { ScsProblemError } from './problem';

describe('apiInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([apiInterceptor])),
        provideHttpClientTesting(),
        {
          provide: CONFIG_FETCH,
          useValue: async () => ({ environment: 'local', apiBaseUrl: 'http://localhost:5080' }),
        },
      ],
    });
    await TestBed.inject(RuntimeConfigService).load();
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
    TestBed.inject(DOCUMENT).documentElement.lang = 'ar';
  });

  afterEach(() => backend.verify());

  it('adds the API address, a reference and the language', () => {
    http.get('/v1/meta/version').subscribe();

    const call = backend.expectOne('http://localhost:5080/v1/meta/version');
    expect(call.request.headers.get('traceparent')).toMatch(/^00-[0-9a-f]{32}-[0-9a-f]{16}-01$/);
    expect(call.request.headers.get('Accept-Language')).toBe('ar');
    call.flush({});
  });

  it('sends English when the page is in English', () => {
    TestBed.inject(DOCUMENT).documentElement.lang = 'en';

    http.get('/v1/meta/version').subscribe();

    const call = backend.expectOne('http://localhost:5080/v1/meta/version');
    expect(call.request.headers.get('Accept-Language')).toBe('en');
    call.flush({});
  });

  it('leaves other requests alone', () => {
    http.get('/config.json').subscribe();

    const call = backend.expectOne('/config.json');
    expect(call.request.headers.has('traceparent')).toBe(false);
    call.flush({});
  });

  it('turns a platform failure into its problem', async () => {
    const result = firstValueFrom(http.get('/v1/meta/version'));
    const problem = {
      type: 'urn:scs:error:internal-error',
      title: 'حدث خطأ غير متوقع',
      status: 500,
      code: 'INTERNAL_ERROR',
      traceId: '4bf92f3577b34da6a3ce929d0e0e4736',
    };

    backend
      .expectOne('http://localhost:5080/v1/meta/version')
      .flush(problem, { status: 500, statusText: 'Server Error' });

    const error = await result.catch((e: unknown) => e);
    expect(error).toBeInstanceOf(ScsProblemError);
    expect((error as ScsProblemError).problem).toEqual(problem);
    expect((error as ScsProblemError).local).toBe(false);
  });

  it('turns anything else into SERVICE_UNAVAILABLE with the request reference', async () => {
    const result = firstValueFrom(http.get('/v1/meta/version'));

    const call = backend.expectOne('http://localhost:5080/v1/meta/version');
    const sent = call.request.headers.get('traceparent')!.split('-')[1];
    call.flush('<html>Bad gateway</html>', { status: 502, statusText: 'Bad Gateway' });

    const error = (await result.catch((e: unknown) => e)) as ScsProblemError;
    expect(error.problem.code).toBe('SERVICE_UNAVAILABLE');
    expect(error.problem.traceId).toBe(sent);
    expect(error.local).toBe(true);
  });
});
