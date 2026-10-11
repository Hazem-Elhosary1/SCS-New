import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, Routes, provideRouter } from '@angular/router';
import { SCS_APP } from '../app-token';
import { CONFIG_FETCH, RuntimeConfigService } from '../config/runtime-config';
import { apiInterceptor } from '../http/api.interceptor';
import { AuthService, sessionInterceptor, sessionMatchGuard, signedOutGuard } from './auth.service';

const API = 'http://localhost:5080';

@Component({ template: '' })
class Blank {}

/**
 * Spec 007, FR-041: the console is downloaded on demand, so "is someone signed in?" is asked
 * before the download, not after it.
 */
describe('the session check made before a part of the app is downloaded', () => {
  let backend: HttpTestingController;
  let router: Router;
  let downloads: number;

  async function setup(): Promise<void> {
    downloads = 0;
    const inside: Routes = [{ path: '**', component: Blank }];
    TestBed.configureTestingModule({
      providers: [
        { provide: SCS_APP, useValue: 'clinic' },
        provideHttpClient(withInterceptors([sessionInterceptor, apiInterceptor])),
        provideHttpClientTesting(),
        provideRouter([
          { path: 'sign-in', component: Blank, canActivate: [signedOutGuard] },
          { path: 'change-password', component: Blank },
          {
            path: '',
            canMatch: [sessionMatchGuard],
            loadChildren: () => {
              downloads += 1;
              return Promise.resolve(inside);
            },
          },
        ]),
        { provide: CONFIG_FETCH, useValue: async () => ({ environment: 'local', apiBaseUrl: API }) },
      ],
    });
    await TestBed.inject(RuntimeConfigService).load();
    backend = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
  }

  const settle = () => new Promise((resolve) => setTimeout(resolve));

  async function arrive(url: string, answer: object, status = 200): Promise<void> {
    const going = router.navigateByUrl(url);
    await settle();
    backend.expectOne(`${API}/v1/auth/session`).flush(answer, { status, statusText: '' });
    await going;
  }

  afterEach(() => {
    backend.verify();
    TestBed.inject(AuthService).ngOnDestroy();
  });

  it('shows the form to someone who is not signed in, and downloads nothing', async () => {
    await setup();

    await arrive(
      '/bookings',
      { type: 'urn:scs:error:x', title: 'x', status: 401, code: 'UNAUTHENTICATED', traceId: 'a'.repeat(32) },
      401,
    );

    expect(router.url).toBe('/sign-in');
    expect(downloads).toBe(0);
  });

  it('lets a signed-in person in, downloading once', async () => {
    await setup();

    await arrive('/bookings', {
      accountId: '0199c0de-0000-7000-8000-000000000001',
      email: 'owner@noor.example',
      clinic: { nameAr: 'عيادة النور', nameEn: 'Al Noor Clinic', state: 'active' },
      expiresAt: '2030-01-01T12:00:00Z',
    });
    await router.navigateByUrl('/patients');

    expect(router.url).toBe('/patients');
    expect(downloads).toBe(1);
  });

  it('takes someone with a temporary password to the page that replaces it, downloading nothing', async () => {
    await setup();

    await arrive('/bookings', {
      accountId: '0199c0de-0000-7000-8000-000000000001',
      email: 'temp@noor.example',
      clinic: { nameAr: 'عيادة النور', nameEn: 'Al Noor Clinic', state: 'active' },
      expiresAt: '2030-01-01T12:00:00Z',
      mustChangePassword: true,
    });

    expect(router.url).toBe('/change-password');
    expect(downloads).toBe(0);
  });
});
