import { inject } from '@angular/core';
import { CanActivateFn, CanMatchFn, Route, Router, Routes } from '@angular/router';
import { AccessService, ClinicTraitsSource, PlanSource, RuntimeConfigService, holds } from 'core';
import {
  DESTINATIONS,
  Destination,
  PARENTS,
  childrenOf,
  pageOf,
  registerDestinationPage,
} from './destinations';
import { NoAccessPage, NotFoundPage, NotInPlanPage, NotYetDestinationPage } from './pages';
import { ShellComponent } from './shell.component';

// The destinations that have a page so far. A later spec adds its line here and nothing else.
// A page is named by how to load it: it is downloaded when it is first opened, so the frame
// does not grow with every destination that gets its page (spec 007, FR-041).
registerDestinationPage('administration.roles', () =>
  import('../administration/roles.page').then((file) => file.RolesPage),
);

const notProduction: CanMatchFn = () => inject(RuntimeConfigService).value.environment !== 'production';
const dentalClinic: CanMatchFn = () => inject(ClinicTraitsSource).dental();
const linkedToDoctor: CanMatchFn = () => inject(ClinicTraitsSource).linkedToDoctor();

function outsidePlan(key: string): CanMatchFn {
  return () => inject(PlanSource).excludes(key);
}

/** Sends the person on to the first child of a parent they may open; stays when there is none. */
function leadsToFirstChild(parent: string): CanActivateFn {
  return () => {
    const access = inject(AccessService);
    const child = childrenOf(parent).find(
      (each) => each.permission === null || access.holds(each.permission),
    );
    return child === undefined ? true : inject(Router).parseUrl(`/${child.path}`);
  };
}

/**
 * A destination is up to three routes on one path, tried in order (spec 006, FR-005 to
 * FR-008): "not in your plan" for a holder whose clinic's plan leaves it out; its page — or
 * "not available yet" — for a holder of its view permission; and "not in your role" for anyone
 * else, who then loads nothing of it (spec 005). A destination that exists only for some
 * clinics or accounts is not an address at all for the others.
 *
 * The three small pages of the frame itself are part of the frame: opening one downloads
 * nothing more, so "not in your role" appears at once (spec 005, SC-008).
 */
function destinationRoutes(place: Destination): Routes {
  const data = { destination: place.key };
  const exists: CanMatchFn[] =
    place.needs === 'dental'
      ? [dentalClinic]
      : place.key === 'account.doctor-profile'
        ? [linkedToDoctor]
        : [];
  const allowed = place.permission === null ? exists : [...exists, holds(place.permission)];
  const load = pageOf(place.key);
  const page: Route =
    load === undefined
      ? { path: place.path, pathMatch: 'full', component: NotYetDestinationPage, canMatch: allowed, data }
      : { path: place.path, pathMatch: 'full', loadComponent: load, canMatch: allowed, data };
  const routes: Routes = [
    {
      path: place.path,
      pathMatch: 'full',
      component: NotInPlanPage,
      canMatch: [...allowed, outsidePlan(place.key)],
      data,
    },
    page,
  ];
  if (place.permission !== null) {
    routes.push({ path: place.path, pathMatch: 'full', component: NoAccessPage, canMatch: exists, data });
  }
  return routes;
}

/**
 * Everything inside the console's frame (spec 006), generated from the one list of
 * destinations the side bar is drawn from. Loaded when a signed-in person first arrives: the
 * sign-in form does not wait for it.
 */
export const SHELL_ROUTES: Routes = [
  {
    path: '',
    component: ShellComponent,
    children: [
      ...DESTINATIONS.flatMap(destinationRoutes),
      // A parent has no page of its own: its address leads to the first child the person may open.
      // With no such child the address is not one of theirs.
      ...PARENTS.map((parent) => ({
        path: parent.path,
        pathMatch: 'full' as const,
        component: NotFoundPage,
        canActivate: [leadsToFirstChild(parent.key)],
      })),
      // For people building the product; these addresses do not exist in production.
      // They are loaded when opened, so production never downloads them.
      {
        path: 'dev/sample-list',
        loadComponent: () => import('../dev/sample-list.page').then((file) => file.SampleListPage),
        canMatch: [notProduction],
      },
      {
        path: 'dev/files',
        loadComponent: () => import('../dev/files.page').then((file) => file.DevFilesPage),
        canMatch: [notProduction],
      },
      {
        path: 'dev/platform',
        loadComponent: () => import('../dev/platform.page').then((file) => file.PlatformPage),
        canMatch: [notProduction],
      },
      { path: '**', component: NotFoundPage },
    ],
  },
];
