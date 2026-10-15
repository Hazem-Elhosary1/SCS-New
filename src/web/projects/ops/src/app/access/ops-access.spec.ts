import { HttpClient, HttpErrorResponse, HttpInterceptorFn, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { LanguageService, ScsProblemError } from 'core';
import { catchError, throwError } from 'rxjs';
import { OpsAuthService, opsSessionInterceptor } from '../auth/ops-auth.service';
import { activitySentence } from '../operators/activity-sentences';
import { neededBy, withNeeded } from '../operators/role-form.page';
import { initials } from '../operators/operators.service';
import { OPS_PERMISSIONS, OPS_PERMISSION_GROUPS, OpsAccess, inCatalogueOrder } from './ops-access.service';
import { OpsCanDirective, guarded } from './ops-can';

const SESSION = '/v1/ops/auth/session';

const session = (permissions: string[], role: { id: string; name: string; systemKey: string | null }) => ({
  operator: { id: 'o-1', fullName: 'أحمد سامي', language: 'ar', roleName: role.name, role },
  expiresAt: '2030-01-01T00:00:00Z',
  idleMinutes: 30,
  permissions,
});

const VIEWER = { id: 'r-3', name: '', systemKey: 'viewer' };
const CUSTOM = { id: 'r-9', name: 'وكيل دعم فني', systemKey: null };

/** What the application's own layer does before the console sees an answer: a refusal becomes a problem. */
const asProblem: HttpInterceptorFn = (request, next) =>
  next(request).pipe(
    catchError((error: unknown) =>
      throwError(() => (error instanceof HttpErrorResponse ? new ScsProblemError(error.error, false) : error)),
    ),
  );

/** Lets what was started reach the point where it asks the platform. */
const settle = () => new Promise((resolve) => setTimeout(resolve));

@Component({ template: 'the team' })
class TeamPage {}

@Component({ template: 'not yours' })
class Elsewhere {}

@Component({
  imports: [OpsCanDirective],
  template: `<button *appCan="'operators.manage'" data-testid="add">add</button>`,
})
class Host {}

/** Spec 011, FR-006 to FR-009 (research R14): what the console draws and opens follows the role. */
describe('what the operator may see and do in the console', () => {
  function start() {
    // The console's language is its own subject; here it is a value that can be switched.
    const spoken = signal<'ar' | 'en'>('ar');
    TestBed.configureTestingModule({
      providers: [
        { provide: LanguageService, useValue: { use: (language: 'ar' | 'en') => spoken.set(language), language: spoken } },
        provideHttpClient(withInterceptors([opsSessionInterceptor, asProblem])),
        provideHttpClientTesting(),
        provideRouter([
          ...guarded('operators', 'operators.view', () => Promise.resolve(TeamPage)).map((route, index) =>
            index === 1 ? { ...route, loadComponent: () => Promise.resolve(Elsewhere) } : route,
          ),
        ]),
      ],
    });
    return {
      auth: TestBed.inject(OpsAuthService),
      access: TestBed.inject(OpsAccess),
      http: TestBed.inject(HttpTestingController),
      language: TestBed.inject(LanguageService),
    };
  }

  async function signIn(parts: ReturnType<typeof start>, body: ReturnType<typeof session>): Promise<void> {
    const loading = parts.auth.load();
    parts.http.expectOne(SESSION).flush(body);
    await loading;
  }

  it('reads the catalogue the platform wrote: 28 permissions in nine groups', () => {
    expect(OPS_PERMISSIONS).toHaveLength(28);
    expect(OPS_PERMISSION_GROUPS.map((group) => group.key)).toEqual([
      'dashboard', 'clinics', 'tickets', 'notices', 'billing', 'operators', 'reports', 'security', 'settings',
    ]);
    expect(inCatalogueOrder(['settings.edit', 'clinics.view', 'clinics.view'])).toEqual(['clinics.view', 'settings.edit']);
  });

  it('knows the codes of the role and names a system role in the page’s language', async () => {
    const parts = start();
    await signIn(parts, session(['operators.view', 'tickets.view'], VIEWER));

    expect(parts.access.can('operators.view')).toBe(true);
    expect(parts.access.can('operators.manage')).toBe(false);
    expect(parts.access.isSuperAdmin()).toBe(false);
    expect(parts.access.roleName()).toBe('مشاهد فقط');
    parts.language.use('en');
    expect(parts.access.roleName()).toBe('Viewer Only');
    expect(parts.access.nameOfRole(CUSTOM)).toBe('وكيل دعم فني');
    expect(parts.access.nameOfPermission('notices.send')).toBe('Send Notices to Clinics');
    expect(parts.access.nameOfGroup('billing')).toBe('Plans & Subscriptions');
    expect(parts.access.nameOfPermission('no.such')).toBe('no.such');
  });

  it('holds nothing before anyone is signed in', () => {
    const { access } = start();
    expect(access.can('operators.view')).toBe(false);
    expect(access.role()).toBeNull();
    expect(access.roleName()).toBe('');
  });

  it('opens a page for whoever holds its permission, after reading the session again', async () => {
    const parts = start();
    await signIn(parts, session(['operators.view'], VIEWER));
    const router = TestBed.inject(Router);

    const going = router.navigateByUrl('/operators');
    await settle();
    parts.http.expectOne(SESSION).flush(session(['operators.view'], VIEWER));
    await going;
    expect(router.routerState.snapshot.root.firstChild?.component).toBe(TeamPage);
  });

  it('shows the other page at the same address when the role lost the permission meanwhile', async () => {
    const parts = start();
    await signIn(parts, session(['operators.view'], VIEWER));
    const router = TestBed.inject(Router);

    const going = router.navigateByUrl('/operators');
    await settle();
    parts.http.expectOne(SESSION).flush(session(['tickets.view'], CUSTOM));
    await going;
    expect(router.url).toBe('/operators');
    expect(router.routerState.snapshot.root.firstChild?.component).toBe(Elsewhere);
    expect(parts.access.roleName()).toBe('وكيل دعم فني');
  });

  it('reads the session again when the platform refuses for a missing permission', async () => {
    const parts = start();
    await signIn(parts, session(['operators.view'], VIEWER));

    TestBed.inject(HttpClient).get('/v1/ops/operators').subscribe({ error: () => undefined });
    parts.http.expectOne('/v1/ops/operators').flush(
      { type: 'x', title: 'x', status: 403, code: 'PERMISSION_REQUIRED', traceId: 't', permission: 'operators.view' },
      { status: 403, statusText: 'Forbidden' },
    );
    await settle();
    parts.http.expectOne(SESSION).flush(session([], CUSTOM));
    await settle();
    expect(parts.access.can('operators.view')).toBe(false);
  });

  it('draws a control only for whoever holds its permission, and takes it away when the role changes', async () => {
    const parts = start();
    await signIn(parts, session(['operators.view', 'operators.manage'], CUSTOM));
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[data-testid="add"]')).not.toBeNull();

    const refreshing = parts.auth.refresh();
    parts.http.expectOne(SESSION).flush(session(['operators.view'], CUSTOM));
    await refreshing;
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[data-testid="add"]')).toBeNull();
  });
});

/** Spec 011: the small rules the team's screens share. */
describe('the small pieces of the team’s screens', () => {
  it('writes initials from the first two words of a name', () => {
    expect(initials('سارة أحمد')).toBe('سأ');
    expect(initials('Nour Khaled Hassan')).toBe('NK');
    expect(initials('  ')).toBe('');
  });

  it('keeps what a permission needs, and says what needs it', () => {
    expect([...withNeeded(['notices.send', 'tickets.viewAll'])].sort()).toEqual(
      ['clinics.view', 'notices.send', 'tickets.view', 'tickets.viewAll'].sort(),
    );
    expect(neededBy('clinics.view', new Set(['clinics.view', 'clinics.edit', 'notices.send']))).toEqual(['clinics.edit', 'notices.send']);
    expect(neededBy('clinics.view', new Set(['clinics.view']))).toEqual([]);
    expect(neededBy('security.manage', new Set(OPS_PERMISSIONS.map((permission) => permission.code)))).toEqual([]);
  });

  it('writes a line of activity as a sentence, and an unknown kind as something a person can read', () => {
    const said: [string, Record<string, string>][] = [];
    const translate = (key: string, values: Record<string, string>) => {
      said.push([key, values]);
      return key;
    };
    const roleName = (role: { name: string; systemKey: string | null }) => (role.systemKey === 'viewer' ? 'مشاهد فقط' : role.name);
    const line = { kind: 'operator.invited', name: 'نور خالد', role: null, roleKey: 'viewer', device: null, atUtc: '2026-10-13T09:00:00Z' };

    activitySentence(line, translate, roleName);
    activitySentence({ ...line, kind: 'operator.role-changed', role: 'وكيل دعم فني', roleKey: null }, translate, roleName);
    activitySentence({ ...line, kind: 'signed-in', name: null, device: 'Chrome على Windows' }, translate, roleName);
    activitySentence({ ...line, kind: 'clinic.suspended' }, translate, roleName);

    expect(said.map(([key]) => key)).toEqual([
      'app.operators.activity.operator.invited',
      'app.operators.activity.operator.role-changed',
      'app.operators.activity.signed-in',
      'app.operators.activity.other',
    ]);
    expect(said[0][1]).toEqual({ name: 'نور خالد', device: '', role: 'مشاهد فقط' });
    expect(said[1][1]['role']).toBe('وكيل دعم فني');
    expect(said[2][1]['device']).toBe('Chrome على Windows');
  });
});
