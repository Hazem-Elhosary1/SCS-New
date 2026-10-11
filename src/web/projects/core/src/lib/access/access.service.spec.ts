import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { SCS_APP } from '../app-token';
import { AuthService, sessionGuard, sessionInterceptor } from '../auth/auth.service';
import { CONFIG_FETCH, RuntimeConfigService } from '../config/runtime-config';
import { apiInterceptor } from '../http/api.interceptor';
import { LanguageService } from '../i18n/language.service';
import {
  ACCESS_REFRESH_MS,
  AccessService,
  PERMISSIONS,
  PERMISSION_GROUPS,
  ScsCanDirective,
  holds,
} from './access.service';

const API = 'http://localhost:5080';

const base = {
  accountId: '0199c0de-0000-7000-8000-000000000001',
  email: 'doctor@shifa.example',
  clinic: { nameAr: 'عيادة الشفاء', nameEn: 'Al Shifa Clinic', state: 'active' },
  expiresAt: '2030-01-01T12:00:00Z',
  mustChangePassword: false,
};

const doctor = { id: 'r-doctor', name: 'طبيب', readyKey: 'doctor' };
const custom = { id: 'r-team', name: 'فريق العيادة', readyKey: null };

function sessionWith(role: object | null, ...permissions: string[]) {
  return { ...base, role, permissions };
}

@Component({ selector: 'scs-test-destination', template: 'roles' })
class Destination {}

@Component({ selector: 'scs-test-no-access', template: 'not in your role' })
class NoAccess {}

@Component({ selector: 'scs-test-home', template: 'home' })
class Home {}

@Component({
  selector: 'scs-test-screen',
  imports: [ScsCanDirective],
  template: `
    <button *scsCan="code()" data-testid="manage">Create role</button>
    <span data-testid="always">roles</span>
  `,
})
class Screen {
  code = signal('admin.roles.manage');
}

describe('AccessService', () => {
  let auth: AuthService;
  let access: AccessService;
  let backend: HttpTestingController;
  let router: Router;

  async function setup(): Promise<void> {
    TestBed.configureTestingModule({
      imports: [
        TranslocoTestingModule.forRoot({
          langs: { ar: {}, en: {} },
          translocoConfig: { availableLangs: ['ar', 'en'], defaultLang: 'ar' },
        }),
      ],
      providers: [
        { provide: SCS_APP, useValue: 'clinic' },
        provideHttpClient(withInterceptors([sessionInterceptor, apiInterceptor])),
        provideHttpClientTesting(),
        provideRouter([
          { path: 'sign-in', component: Home },
          {
            path: 'administration/roles',
            component: Destination,
            canActivate: [sessionGuard],
            canMatch: [holds('admin.roles.view')],
          },
          { path: 'administration/roles', component: NoAccess, canActivate: [sessionGuard] },
          { path: '**', component: Home, canActivate: [sessionGuard] },
        ]),
        { provide: CONFIG_FETCH, useValue: async () => ({ environment: 'local', apiBaseUrl: API }) },
      ],
    });
    await TestBed.inject(RuntimeConfigService).load();
    auth = TestBed.inject(AuthService);
    access = TestBed.inject(AccessService);
    backend = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
  }

  const settle = () => new Promise((resolve) => setTimeout(resolve));

  /** Answers the one question "who is signed in". */
  async function signedInAs(session: object): Promise<void> {
    const loading = auth.load();
    backend.expectOne(`${API}/v1/auth/session`).flush(session);
    await loading;
    TestBed.tick();
  }

  /** The component the router is showing. */
  function shown(): unknown {
    return router.routerState.snapshot.root.firstChild?.component;
  }

  afterEach(() => {
    backend.verify();
    TestBed.inject(AccessService).ngOnDestroy();
    TestBed.inject(AuthService).ngOnDestroy();
    vi.useRealTimers();
  });

  it('carries the catalogue the platform holds: 83 permissions in 11 groups', async () => {
    await setup();

    expect(PERMISSIONS.length).toBe(83);
    expect(PERMISSIONS.map((permission) => permission.number)).toEqual(
      Array.from({ length: 83 }, (_, index) => index + 1),
    );
    expect(PERMISSION_GROUPS.map((group) => group.key)).toEqual([
      'home',
      'bookings',
      'patients',
      'money',
      'inbox',
      'tickets',
      'reports',
      'admin',
      'data',
      'settings',
      'subscription',
    ]);
    expect(PERMISSIONS.filter((permission) => permission.group === 'home')).toEqual([]);
  });

  it('holds what the session lists and nothing else', async () => {
    await setup();
    await signedInAs(sessionWith(doctor, 'bookings.view', 'admin.roles.view'));

    expect(access.holds('admin.roles.view')).toBe(true);
    expect(access.holds('admin.roles.manage')).toBe(false);
    expect(access.permissions()).toEqual(['bookings.view', 'admin.roles.view']);
  });

  it('holds nothing for an account without a role, and nothing while nobody is signed in', async () => {
    await setup();
    expect(access.holds('bookings.view')).toBe(false);

    await signedInAs(sessionWith(null));

    expect(access.role()).toBeNull();
    expect(access.roleNameKey()).toBeNull();
    expect(access.holds('bookings.view')).toBe(false);
  });

  it('names a permission in the language of the page', async () => {
    await setup();

    expect(access.nameOf('admin.roles.view')).toBe('عرض الأدوار والصلاحيات');
    TestBed.inject(LanguageService).use('en');
    expect(access.nameOf('admin.roles.view')).toBe('View roles & permissions');
    expect(access.nameOf('no.such.code')).toBe('no.such.code');
  });

  it('gives a ready role its name in the page language until the clinic renames it', async () => {
    await setup();
    await signedInAs(sessionWith(doctor, 'bookings.view'));
    expect(access.roleNameKey()).toBe('ui.access.role.doctor');

    // Renamed by the clinic: the platform stops sending the key, and the name is shown as written.
    const asking = auth.refreshSession();
    backend
      .expectOne(`${API}/v1/auth/session`)
      .flush(sessionWith({ id: 'r-doctor', name: 'طبيب أسنان', readyKey: null }, 'bookings.view'));
    await asking;

    expect(access.roleNameKey()).toBeNull();
    expect(access.role()?.name).toBe('طبيب أسنان');
  });

  it('shows a custom role as the clinic wrote it', async () => {
    await setup();
    await signedInAs(sessionWith(custom, 'bookings.view'));

    expect(access.roleNameKey()).toBeNull();
    expect(access.role()?.name).toBe('فريق العيادة');
  });

  it('asks again every ten minutes while somebody is signed in, and stops when they leave', async () => {
    vi.useFakeTimers();
    await setup();
    const loading = auth.load();
    backend.expectOne(`${API}/v1/auth/session`).flush(sessionWith(doctor, 'admin.roles.view'));
    await loading;
    TestBed.tick();

    expect(ACCESS_REFRESH_MS).toBeLessThanOrEqual(15 * 60 * 1000);

    vi.advanceTimersByTime(ACCESS_REFRESH_MS);
    backend.expectOne(`${API}/v1/auth/session`).flush(sessionWith(doctor, 'bookings.view'));
    await vi.advanceTimersByTimeAsync(0);

    // The role was edited meanwhile: the screen follows.
    expect(access.holds('admin.roles.view')).toBe(false);
    expect(access.holds('bookings.view')).toBe(true);

    const leaving = auth.signOut();
    backend.expectOne(`${API}/v1/auth/sign-out`).flush(null);
    await leaving;
    TestBed.tick();

    vi.advanceTimersByTime(ACCESS_REFRESH_MS * 2);
    backend.expectNone(`${API}/v1/auth/session`);
  });

  it('keeps what it knows when asking again fails for a reason that is not a refusal', async () => {
    await setup();
    await signedInAs(sessionWith(doctor, 'admin.roles.view'));

    const asking = auth.refreshSession();
    backend.expectOne(`${API}/v1/auth/session`).error(new ProgressEvent('error'));
    await asking;

    expect(auth.status()).toBe('signed-in');
    expect(access.holds('admin.roles.view')).toBe(true);
  });

  describe('holds() on a route', () => {
    it('opens the destination for a holder of its view', async () => {
      await setup();
      await signedInAs(sessionWith(doctor, 'admin.roles.view'));

      await router.navigateByUrl('/administration/roles');

      expect(shown()).toBe(Destination);
    });

    it('gives the same address to the no-access page for anyone else', async () => {
      await setup();
      await signedInAs(sessionWith(custom, 'bookings.view'));

      await router.navigateByUrl('/administration/roles');

      expect(shown()).toBe(NoAccess);
      expect(router.url).toBe('/administration/roles');
    });

    it('opens the home page for a person with no role at all', async () => {
      await setup();
      await signedInAs(sessionWith(null));

      await router.navigateByUrl('/');

      expect(shown()).toBe(Home);
    });

    it('sends a person who is not signed in to the sign-in form', async () => {
      await setup();
      const going = router.navigateByUrl('/administration/roles');
      await settle();
      backend
        .expectOne(`${API}/v1/auth/session`)
        .flush(
          { type: 'urn:scs:error:x', title: 'x', status: 401, code: 'UNAUTHENTICATED', traceId: 'a'.repeat(32) },
          { status: 401, statusText: 'Unauthorized' },
        );
      await going;

      expect(router.url).toBe('/sign-in');
    });
  });

  describe('a refusal by the platform', () => {
    it('reads the list again and matches the address again, and still reaches the caller', async () => {
      await setup();
      await signedInAs(sessionWith(doctor, 'admin.roles.view'));
      await router.navigateByUrl('/administration/roles');
      expect(shown()).toBe(Destination);

      // The role lost the view a moment ago; the page still asks for its content.
      let failure: unknown = null;
      TestBed.inject(HttpClient)
        .get('/v1/roles')
        .subscribe({ error: (error: unknown) => (failure = error) });
      backend.expectOne(`${API}/v1/roles`).flush(
        {
          type: 'urn:scs:error:permission-required',
          title: 'هذه الصفحة ليست في دورك',
          status: 403,
          code: 'PERMISSION_REQUIRED',
          permission: 'admin.roles.view',
          traceId: 'a'.repeat(32),
        },
        { status: 403, statusText: 'Forbidden' },
      );
      await settle();
      backend.expectOne(`${API}/v1/auth/session`).flush(sessionWith(doctor, 'bookings.view'));
      await settle();
      await settle();

      expect(failure).not.toBeNull();
      expect(access.holds('admin.roles.view')).toBe(false);
      expect(shown()).toBe(NoAccess);
      expect(router.url).toBe('/administration/roles');
    });
  });

  describe('*scsCan', () => {
    it('leaves a control out of the page for a person without the permission — not disabled', async () => {
      await setup();
      await signedInAs(sessionWith(doctor, 'admin.roles.view'));
      const fixture = TestBed.createComponent(Screen);
      fixture.detectChanges();
      const element = fixture.nativeElement as HTMLElement;

      expect(element.querySelector('[data-testid="manage"]')).toBeNull();
      expect(element.querySelector('[disabled]')).toBeNull();
      expect(element.querySelector('[data-testid="always"]')).not.toBeNull();
    });

    it('shows it to a holder, and follows a change to what the person holds', async () => {
      await setup();
      await signedInAs(sessionWith(doctor, 'admin.roles.view', 'admin.roles.manage'));
      const fixture = TestBed.createComponent(Screen);
      fixture.detectChanges();
      const element = fixture.nativeElement as HTMLElement;
      expect(element.querySelectorAll('[data-testid="manage"]').length).toBe(1);

      const asking = auth.refreshSession();
      backend.expectOne(`${API}/v1/auth/session`).flush(sessionWith(doctor, 'admin.roles.view'));
      await asking;
      fixture.detectChanges();
      expect(element.querySelector('[data-testid="manage"]')).toBeNull();

      const again = auth.refreshSession();
      backend
        .expectOne(`${API}/v1/auth/session`)
        .flush(sessionWith(doctor, 'admin.roles.view', 'admin.roles.manage'));
      await again;
      fixture.detectChanges();
      expect(element.querySelectorAll('[data-testid="manage"]').length).toBe(1);
    });
  });
});
