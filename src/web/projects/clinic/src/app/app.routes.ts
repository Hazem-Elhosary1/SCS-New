import { Routes } from '@angular/router';
import { pendingSignInGuard, sessionGuard, signedOutGuard, temporaryPasswordGuard } from 'core';
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
  { path: '**', component: LandingPage, canActivate: [sessionGuard] },
];
