import { inject } from '@angular/core';
import { CanActivateFn, CanMatchFn, Router, Routes } from '@angular/router';
import {
  AccessService,
  ClinicTraitsSource,
  PlanSource,
  RuntimeConfigService,
  holds,
  pendingSignInGuard,
  sessionGuard,
  signedOutGuard,
  temporaryPasswordGuard,
} from 'core';
import { RolesPage } from './administration/roles.page';
import { ChangePasswordPage } from './password/change-password.page';
import { ForgotPasswordPage } from './password/forgot-password.page';
import { SetPasswordPage } from './password/set-password.page';
import {
  DESTINATIONS,
  Destination,
  PARENTS,
  childrenOf,
  pageOf,
  registerDestinationPage,
} from './shell/destinations';
import {
  NoAccessPage,
  NotFoundPage,
  NotInPlanPage,
  NotYetDestinationPage,
  NotYetPage,
} from './shell/pages';
import { ShellComponent } from './shell/shell.component';
import { CodePage } from './sign-in/code.page';
import { SignInPage } from './sign-in/sign-in.page';

// The destinations that have a page so far. A later spec adds its line here and nothing else.
registerDestinationPage('administration.roles', RolesPage);

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
  const routes: Routes = [
    {
      path: place.path,
      pathMatch: 'full',
      component: NotInPlanPage,
      canMatch: [...allowed, outsidePlan(place.key)],
      data,
    },
    {
      path: place.path,
      pathMatch: 'full',
      component: pageOf(place.key) ?? NotYetDestinationPage,
      canMatch: allowed,
      data,
    },
  ];
  if (place.permission !== null) {
    routes.push({ path: place.path, pathMatch: 'full', component: NoAccessPage, canMatch: exists, data });
  }
  return routes;
}

/**
 * Everything in the console needs a signed-in person; without one the sign-in form is shown
 * (spec 003, FR-042). The pages reachable before sign-in are listed here and nowhere else. A
 * person holding a temporary password is taken to the one page that replaces it (spec 004).
 * Every other address is inside the console's frame (spec 006), generated from the one list of
 * destinations the side bar is drawn from.
 */
export const routes: Routes = [
  { path: 'sign-in', component: SignInPage, canActivate: [signedOutGuard] },
  { path: 'sign-in/code', component: CodePage, canActivate: [pendingSignInGuard] },
  { path: 'forgot-password', component: ForgotPasswordPage },
  // Opened from an emailed link, by anyone: a session on this browser is neither used nor ended.
  { path: 'set-password', component: SetPasswordPage },
  { path: 'change-password', component: ChangePasswordPage, canActivate: [temporaryPasswordGuard] },
  { path: 'privacy', component: NotYetPage },
  { path: 'terms', component: NotYetPage },
  {
    path: '',
    component: ShellComponent,
    canActivate: [sessionGuard],
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
        loadComponent: () => import('./dev/sample-list.page').then((file) => file.SampleListPage),
        canMatch: [notProduction],
      },
      {
        path: 'dev/platform',
        loadComponent: () => import('./dev/platform.page').then((file) => file.PlatformPage),
        canMatch: [notProduction],
      },
      { path: '**', component: NotFoundPage },
    ],
  },
];
