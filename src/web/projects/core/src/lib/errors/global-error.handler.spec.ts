import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component, ErrorHandler } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { CONFIG_FETCH, RuntimeConfigService } from '../config/runtime-config';
import { apiInterceptor } from '../http/api.interceptor';
import { ScsProblemError } from '../http/problem';
import { SCS_APP } from '../app-token';
import { VERSION_FETCH, VersionCheckService } from '../version/version-check.service';
import { ERROR_CLOCK, ScsErrorHandler } from './global-error.handler';

@Component({ template: '' })
class BlankPage {}

const REPORTS = '/api/v1/client-errors';

describe('ScsErrorHandler', () => {
  let handler: ErrorHandler;
  let backend: HttpTestingController;
  let time: number;
  let consoleError: typeof console.error;

  beforeEach(async () => {
    time = 1_000_000;
    consoleError = console.error;
    console.error = () => undefined;

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([apiInterceptor])),
        provideHttpClientTesting(),
        provideRouter([
          { path: 'patients/:id', component: BlankPage },
          { path: '**', component: BlankPage },
        ]),
        { provide: SCS_APP, useValue: 'clinic' },
        { provide: ERROR_CLOCK, useValue: () => time },
        { provide: ErrorHandler, useClass: ScsErrorHandler },
        {
          provide: CONFIG_FETCH,
          useValue: async () => ({ environment: 'test', apiBaseUrl: '/api' }),
        },
        {
          provide: VERSION_FETCH,
          useValue: async () => ({
            app: 'clinic',
            version: '0.1.0+3f9c2ab',
            commit: 'a'.repeat(40),
            builtAt: '2026-10-10T09:30:00Z',
          }),
        },
      ],
    });
    await TestBed.inject(RuntimeConfigService).load();
    await TestBed.inject(VersionCheckService).check();
    handler = TestBed.inject(ErrorHandler);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    console.error = consoleError;
    backend.verify();
  });

  it('reports an unhandled error with the app, version, message and stack', () => {
    handler.handleError(new TypeError('Cannot read properties of undefined'));

    const report = backend.expectOne(REPORTS);
    expect(report.request.method).toBe('POST');
    expect(report.request.body).toMatchObject({
      app: 'clinic',
      version: '0.1.0+3f9c2ab',
      message: 'TypeError: Cannot read properties of undefined',
      language: 'ar',
    });
    expect(report.request.body.stack).toContain('TypeError');
    report.flush(null, { status: 202, statusText: 'Accepted' });
  });

  it('sends the route template, never the address with its values', async () => {
    await TestBed.inject(Router).navigateByUrl('/patients/42?name=Mariam');

    handler.handleError(new Error('boom'));

    const report = backend.expectOne(REPORTS);
    expect(report.request.body.route).toBe('/patients/:id');
    expect(JSON.stringify(report.request.body)).not.toContain('42');
    expect(JSON.stringify(report.request.body)).not.toContain('Mariam');
    report.flush(null);
  });

  it('sends only the fields the platform accepts', () => {
    handler.handleError(new Error('boom'));

    const report = backend.expectOne(REPORTS);
    const allowed = ['app', 'version', 'message', 'route', 'stack', 'traceId', 'language'];
    expect(Object.keys(report.request.body).every((key) => allowed.includes(key))).toBe(true);
    report.flush(null);
  });

  it('reports the same message at most once a minute', () => {
    handler.handleError(new Error('boom'));
    backend.expectOne(REPORTS).flush(null);

    time += 59_000;
    handler.handleError(new Error('boom'));
    backend.expectNone(REPORTS);

    handler.handleError(new Error('something else'));
    backend.expectOne(REPORTS).flush(null);

    time += 1_000;
    handler.handleError(new Error('boom'));
    backend.expectOne(REPORTS).flush(null);
  });

  it('truncates what it sends to the limits of the contract', () => {
    const error = new Error('x'.repeat(5000));
    error.stack = 's'.repeat(20000);

    handler.handleError(error);

    const report = backend.expectOne(REPORTS);
    expect(report.request.body.message.length).toBe(1000);
    expect(report.request.body.stack.length).toBe(8000);
    report.flush(null);
  });

  it('describes an unhandled platform failure by its code and reference only', () => {
    handler.handleError(
      new ScsProblemError(
        {
          type: 'urn:scs:error:forbidden',
          title: 'لا تتوفر صلاحية لهذا الإجراء',
          status: 403,
          code: 'FORBIDDEN',
          traceId: '4bf92f3577b34da6a3ce929d0e0e4736',
        },
        false,
      ),
    );

    const report = backend.expectOne(REPORTS);
    expect(report.request.body.message).toBe(
      'Unhandled platform failure FORBIDDEN (4bf92f3577b34da6a3ce929d0e0e4736)',
    );
    expect(report.request.body.stack).toBeUndefined();
    report.flush(null);
  });

  it('does not raise another report when reporting itself fails', () => {
    handler.handleError(new Error('boom'));

    backend.expectOne(REPORTS).flush('down', { status: 503, statusText: 'Service Unavailable' });

    backend.expectNone(REPORTS);
  });

  it('handles values that are not errors', () => {
    handler.handleError('plain text');
    expect(backend.expectOne(REPORTS).request.body.message).toBe('plain text');

    handler.handleError({ unexpected: true });
    expect(backend.expectOne(REPORTS).request.body.message).toBe('Non-error value thrown');
  });
});
