import { Routes } from '@angular/router';
import {
  holds,
  pendingSignInGuard,
  sessionGuard,
  signedOutGuard,
  temporaryPasswordGuard,
} from 'core';
import { NoAccessPage, ROLES_VIEW, RolesPage } from './administration/roles.page';
import { LandingPage, NotYetPage } from './landing/landing.page';
import { ChangePasswordPage } from './password/change-password.page';
import { ForgotPasswordPage } from './password/forgot-password.page';
import { SetPasswordPage } from './password/set-password.page';
import { CodePage } from './sign-in/code.page';
import { SignInPage } from './sign-in/sign-in.page';

/**
 * Everything in the console needs a signed-in person; without one the sign-in form is shown
 * (spec 003, FR-042). The pages reachable before sign-in are listed here and nowhere else. A
 * person holding a temporary password is taken to the one page that replaces it (spec 004).
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
  // A destination is two routes on one path: its page for a holder of its view permission,
  // and the "not in your role" page for anyone else — who then loads nothing of it (spec 005).
  {
    path: 'administration/roles',
    component: RolesPage,
    canActivate: [sessionGuard],
    canMatch: [holds(ROLES_VIEW)],
  },
  {
    path: 'administration/roles',
    component: NoAccessPage,
    canActivate: [sessionGuard],
    data: { permission: ROLES_VIEW, titleKey: 'app.administration.roles.title' },
  },
  { path: '**', component: LandingPage, canActivate: [sessionGuard] },
];
