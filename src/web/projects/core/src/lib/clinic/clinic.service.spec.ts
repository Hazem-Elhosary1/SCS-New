import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { DOCUMENT } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { SCS_APP, ScsApp } from '../app-token';
import { CONFIG_FETCH, RuntimeConfigService } from '../config/runtime-config';
import { apiInterceptor } from '../http/api.interceptor';
import { ClinicService, clinicFromPath } from './clinic.service';

const CLINIC = '/api/v1/clinic';

const noor = {
  shortName: 'noor',
  nameAr: 'عيادة النور',
  nameEn: 'Al Noor Clinic',
  state: 'active',
  renamed: false,
};

describe('clinicFromPath', () => {
  it('takes the first part of the path, in lower case', () => {
    expect(clinicFromPath('/noor')).toBe('noor');
    expect(clinicFromPath('/noor/')).toBe('noor');
    expect(clinicFromPath('/NOOR/booking/step-2')).toBe('noor');
    expect(clinicFromPath('//al-noor//x')).toBe('al-noor');
    expect(clinicFromPath('/%6Eoor')).toBe('noor');
  });

  it('gives nothing for an address with no clinic', () => {
    expect(clinicFromPath('/')).toBeNull();
    expect(clinicFromPath('')).toBeNull();
  });

  it('gives nothing for text that could not be a clinic name', () => {
    for (const path of [
      '/ab',
      '/1noor',
      '/al_noor',
      '/config.json',
      '/noor-',
      '/النور',
      '/%E0%A4%A',
      `/${'a'.repeat(41)}`,
    ]) {
      expect(clinicFromPath(path)).toBeNull();
    }
  });
});

describe('ClinicService', () => {
  let backend: HttpTestingController;
  let clinic: ClinicService;

  async function setup(path: string, app: ScsApp = 'patient'): Promise<void> {
    const location = { pathname: path };
    const fakeDocument = { documentElement: { lang: 'ar' }, location };

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([apiInterceptor])),
        provideHttpClientTesting(),
        { provide: SCS_APP, useValue: app },
        { provide: DOCUMENT, useValue: fakeDocument },
        {
          provide: CONFIG_FETCH,
          useValue: async () => ({ environment: 'test', apiBaseUrl: '/api' }),
        },
      ],
    });
    await TestBed.inject(RuntimeConfigService).load();
    backend = TestBed.inject(HttpTestingController);
    clinic = TestBed.inject(ClinicService);
  }

  afterEach(() => backend.verify());

  it('asks the platform for the clinic the address names, sending the name as a header', async () => {
    await setup('/Noor/booking');

    const loading = clinic.load();
    const call = backend.expectOne(CLINIC);
    expect(call.request.headers.get('X-Scs-Clinic')).toBe('noor');
    call.flush(noor);

    const status = await loading;
    expect(status).toEqual({ state: 'ready', clinic: noor });
    expect(clinic.status()).toEqual(status);
  });

  it('does not ask the platform when the address names no clinic', async () => {
    await setup('/');

    expect((await clinic.load()).state).toBe('not-available');
    backend.expectNone(CLINIC);
  });

  it('does not ask the platform for text that could not be a name', async () => {
    await setup('/config.json');

    expect((await clinic.load()).state).toBe('not-available');
    backend.expectNone(CLINIC);
  });

  it('shows not-available when the platform says there is no such clinic', async () => {
    await setup('/nosuch');

    const loading = clinic.load();
    backend.expectOne(CLINIC).flush(
      {
        type: 'urn:scs:error:clinic-not-available',
        title: 'هذه الصفحة غير متاحة',
        status: 404,
        code: 'CLINIC_NOT_AVAILABLE',
        traceId: '4bf92f3577b34da6a3ce929d0e0e4736',
      },
      { status: 404, statusText: 'Not Found' },
    );

    expect((await loading).state).toBe('not-available');
  });

  it('reports a failure, not "no such clinic", when the platform cannot be asked', async () => {
    await setup('/noor');

    const loading = clinic.load();
    backend.expectOne(CLINIC).flush('bad gateway', { status: 502, statusText: 'Bad Gateway' });

    const status = await loading;
    expect(status.state).toBe('failed');
    expect(status.state === 'failed' && status.problem.code).toBe('SERVICE_UNAVAILABLE');
  });

  it('sends the clinic header on every platform call from the patient site', async () => {
    await setup('/noor');

    clinic.load();
    expect(backend.expectOne(CLINIC).request.headers.get('X-Scs-Clinic')).toBe('noor');
  });

  it('never sends the clinic header from the staff or operators sites', async () => {
    for (const app of ['clinic', 'ops'] as const) {
      await setup('/noor', app);

      clinic.load();
      expect(backend.expectOne(CLINIC).request.headers.has('X-Scs-Clinic')).toBe(false);

      backend.verify();
      TestBed.resetTestingModule();
    }

    await setup('/noor');
  });
});
