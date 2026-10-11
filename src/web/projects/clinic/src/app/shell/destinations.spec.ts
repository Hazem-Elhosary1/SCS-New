import { Route } from '@angular/router';
import { PERMISSIONS } from 'core';
import { RolesPage } from '../administration/roles.page';
import { routes } from '../app.routes';
import { SignInPage } from '../sign-in/sign-in.page';
import { DESTINATIONS, NAV, PARENTS, childrenOf, destination, pageOf } from './destinations';
import { NoAccessPage, NotFoundPage, NotInPlanPage, NotYetDestinationPage } from './pages';
import { ShellComponent } from './shell.component';
import { SHELL_ROUTES } from './shell.routes';

const ar = (await import('../../../public/i18n/ar.json')).default as unknown;
const en = (await import('../../../public/i18n/en.json')).default as unknown;

function word(bundle: unknown, key: string): unknown {
  return key.split('.').reduce<unknown>((at, part) => (at as Record<string, unknown>)?.[part], bundle);
}

const inside = SHELL_ROUTES[0].children ?? [];
const routesAt = (path: string): Route[] => inside.filter((route) => route.path === path);

describe('the destinations of the console', () => {
  it('names each destination and each address once', () => {
    const keys = DESTINATIONS.map((place) => place.key);
    const paths = DESTINATIONS.map((place) => place.path);

    expect(new Set(keys).size).toBe(keys.length);
    expect(new Set(paths).size).toBe(paths.length);
  });

  it('opens each with a view permission of the catalogue', () => {
    const codes = new Set(PERMISSIONS.map((permission) => permission.code));

    for (const place of DESTINATIONS.filter((each) => each.permission !== null)) {
      expect(codes, place.key).toContain(place.permission);
    }
  });

  it('has twenty in the side bar under three parents, and two in the account menu', () => {
    expect(DESTINATIONS.filter((place) => place.place === 'side-bar')).toHaveLength(20);
    expect(DESTINATIONS.filter((place) => place.place === 'account-menu')).toHaveLength(2);
    expect(PARENTS.map((parent) => childrenOf(parent.key).length)).toEqual([5, 3, 4]);
  });

  it('puts every side-bar destination in the side bar exactly once', () => {
    const top = NAV.flatMap((group) => group.entries);
    const reached = top.flatMap((key) =>
      PARENTS.some((parent) => parent.key === key)
        ? childrenOf(key).map((child) => child.key)
        : [destination(key).key],
    );

    expect([...reached].sort()).toEqual(
      DESTINATIONS.filter((place) => place.place === 'side-bar')
        .map((place) => place.key)
        .sort(),
    );
  });

  it('has a name for every destination, parent and group in both languages', () => {
    const keys = [
      ...DESTINATIONS.map((place) => place.labelKey),
      ...PARENTS.map((parent) => parent.labelKey),
      ...NAV.map((group) => group.labelKey),
    ];

    for (const key of keys) {
      expect(word(ar, key), `ar ${key}`).toEqual(expect.any(String));
      expect(word(en, key), `en ${key}`).toEqual(expect.any(String));
    }
  });

  it('refuses a page for a destination that does not exist', () => {
    expect(() => destination('nowhere')).toThrow();
  });
});

describe('the routes generated from the destinations', () => {
  it('gives every destination its page or "not available yet", behind "not in your plan"', () => {
    for (const place of DESTINATIONS) {
      const [plan, page] = routesAt(place.path);

      expect(plan.component, place.key).toBe(NotInPlanPage);
      if (pageOf(place.key) === undefined) {
        expect(page.component, place.key).toBe(NotYetDestinationPage);
      } else {
        expect(page.loadComponent, place.key).toBe(pageOf(place.key));
        expect(page.component, place.key).toBeUndefined();
      }
      expect(page.data, place.key).toEqual({ destination: place.key });
    }
  });

  it('ends every destination that needs a permission with "not in your role"', () => {
    for (const place of DESTINATIONS) {
      const last = routesAt(place.path).at(-1);

      expect(last?.component === NoAccessPage, place.key).toBe(place.permission !== null);
    }
  });

  it('guards every page by a check when the destination needs a permission', () => {
    for (const place of DESTINATIONS.filter((each) => each.permission !== null)) {
      expect(routesAt(place.path)[1].canMatch?.length, place.key).toBeGreaterThan(0);
    }
  });

  it('shows the roles list at its destination, loaded when it is opened', async () => {
    const page = routesAt('administration/roles')[1];

    expect(await page.loadComponent?.()).toBe(RolesPage);
  });

  it('leads a parent to a child, and anything else to "not found"', () => {
    for (const parent of PARENTS) {
      expect(routesAt(parent.path)[0].canActivate, parent.key).toHaveLength(1);
    }
    expect(inside.at(-1)).toEqual({ path: '**', component: NotFoundPage });
  });

  it('keeps the pages for builders behind the environment', () => {
    for (const path of ['dev/sample-list', 'dev/platform']) {
      expect(routesAt(path)[0].canMatch, path).toHaveLength(1);
    }
  });
});

describe('the routes as the router takes them', () => {
  it.each([
    ['the app', routes],
    ['the frame', SHELL_ROUTES],
  ])('are a configuration the router accepts: %s', async (_name, config) => {
    const { TestBed } = await import('@angular/core/testing');
    const { Router, provideRouter } = await import('@angular/router');
    TestBed.configureTestingModule({ providers: [provideRouter(config)] });

    expect(() => TestBed.inject(Router)).not.toThrow();
  });
});

describe('what is downloaded first', () => {
  // Spec 007, FR-041: a person who has not signed in downloads the sign-in form and nothing else.
  it('is the sign-in form alone; every other page before sign-in is loaded when opened', () => {
    const eager = routes.filter((route) => route.component !== undefined);

    expect(eager.map((route) => route.path)).toEqual(['sign-in']);
    expect(eager[0].component).toBe(SignInPage);
    for (const route of routes.filter((each) => each.path !== 'sign-in' && each.path !== '')) {
      expect(route.loadComponent, route.path).toEqual(expect.any(Function));
    }
  });

  it('loads the frame and everything inside it only for a signed-in person', async () => {
    const frame = routes.find((route) => route.path === '');

    expect(frame?.component).toBeUndefined();
    expect(frame?.children).toBeUndefined();
    // Asked before the download, not after it.
    expect(frame?.canMatch).toHaveLength(1);
    expect(frame?.canActivate).toBeUndefined();
    expect(await frame?.loadChildren?.()).toBe(SHELL_ROUTES);
    expect(SHELL_ROUTES[0].component).toBe(ShellComponent);
  });

  // Spec 007, FR-040: opening one of the frame's own pages costs no further download.
  it('keeps the frame’s own small pages inside the frame', () => {
    const own = [NotInPlanPage, NotYetDestinationPage, NoAccessPage, NotFoundPage];
    const used = new Set(inside.map((route) => route.component).filter((page) => page !== undefined));

    expect([...used].sort()).toEqual([...own].sort());
  });

  it('names a destination’s page by how to load it', () => {
    for (const place of DESTINATIONS) {
      const load = pageOf(place.key);

      expect(load === undefined || typeof load === 'function', place.key).toBe(true);
    }
    expect(pageOf('administration.roles')).toEqual(expect.any(Function));
    expect(pageOf('bookings')).toBeUndefined();
  });
});

describe('the icons of the side bar', () => {
  it('give every destination one, a child of a parent too — as the design draws them', () => {
    for (const place of DESTINATIONS) {
      expect(place.icon, place.key).toEqual(expect.any(String));
    }
  });
});
