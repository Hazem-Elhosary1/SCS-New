import { Routes } from '@angular/router';
import { pendingSignInGuard, sessionMatchGuard, signedOutGuard, temporaryPasswordGuard } from 'core';
import { SignInPage } from './sign-in/sign-in.page';

/**
 * Everything in the console needs a signed-in person; without one the sign-in form is shown
 * (spec 003, FR-042). The pages reachable before sign-in are listed here and nowhere else. A
 * person holding a temporary password is taken to the one page that replaces it (spec 004).
 * Every other address is inside the console's frame (spec 006).
 *
 * Only the sign-in form is part of the first download. Each other page here, and the frame
 * with everything inside it, is downloaded when it is first opened (spec 007, FR-041).
 */
export const routes: Routes = [
  { path: 'sign-in', component: SignInPage, canActivate: [signedOutGuard] },
  {
    path: 'sign-in/code',
    loadComponent: () => import('./sign-in/code.page').then((file) => file.CodePage),
    canActivate: [pendingSignInGuard],
  },
  {
    path: 'forgot-password',
    loadComponent: () => import('./password/forgot-password.page').then((file) => file.ForgotPasswordPage),
  },
  // Opened from an emailed link, by anyone: a session on this browser is neither used nor ended.
  {
    path: 'set-password',
    loadComponent: () => import('./password/set-password.page').then((file) => file.SetPasswordPage),
  },
  {
    path: 'change-password',
    loadComponent: () => import('./password/change-password.page').then((file) => file.ChangePasswordPage),
    canActivate: [temporaryPasswordGuard],
  },
  { path: 'privacy', loadComponent: () => import('./shell/pages').then((file) => file.NotYetPage) },
  { path: 'terms', loadComponent: () => import('./shell/pages').then((file) => file.NotYetPage) },
  {
    path: '',
    // Asked before the frame is downloaded: someone who is not signed in never loads it.
    canMatch: [sessionMatchGuard],
    loadChildren: () => import('./shell/shell.routes').then((file) => file.SHELL_ROUTES),
  },
];
