import { Route } from '@angular/router';
import { PERMISSIONS } from 'core';
import { RolesPage } from '../administration/roles.page';
import { routes } from '../app.routes';
import { DESTINATIONS, NAV, PARENTS, childrenOf, destination, pageOf } from './destinations';
import { NoAccessPage, NotFoundPage, NotInPlanPage, NotYetDestinationPage } from './pages';

const ar = (await import('../../../public/i18n/ar.json')).default as unknown;
const en = (await import('../../../public/i18n/en.json')).default as unknown;

function word(bundle: unknown, key: string): unknown {
  return key.split('.').reduce<unknown>((at, part) => (at as Record<string, unknown>)?.[part], bundle);
}

const inside = routes.find((route) => route.path === '')?.children ?? [];
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
      expect(page.component, place.key).toBe(pageOf(place.key) ?? NotYetDestinationPage);
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

  it('shows the roles list at its destination', () => {
    expect(routesAt('administration/roles')[1].component).toBe(RolesPage);
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
  it('are a configuration the router accepts', async () => {
    const { TestBed } = await import('@angular/core/testing');
    const { Router, provideRouter } = await import('@angular/router');
    TestBed.configureTestingModule({ providers: [provideRouter(routes)] });

    expect(() => TestBed.inject(Router)).not.toThrow();
  });
});

describe('the icons of the side bar', () => {
  it('give every destination one, a child of a parent too — as the design draws them', () => {
    for (const place of DESTINATIONS) {
      expect(place.icon, place.key).toEqual(expect.any(String));
    }
  });
});
