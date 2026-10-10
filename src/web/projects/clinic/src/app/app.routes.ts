import { Routes } from '@angular/router';
import { pendingSignInGuard, sessionGuard, signedOutGuard } from 'core';
import { LandingPage, NotYetPage } from './landing/landing.page';
import { CodePage } from './sign-in/code.page';
import { SignInPage } from './sign-in/sign-in.page';

/**
 * Everything in the console needs a signed-in person; without one the sign-in form is shown
 * (spec 003, FR-042). The pages reachable before sign-in are listed here and nowhere else.
 */
export const routes: Routes = [
  { path: 'sign-in', component: SignInPage, canActivate: [signedOutGuard] },
  { path: 'sign-in/code', component: CodePage, canActivate: [pendingSignInGuard] },
  { path: 'forgot-password', component: NotYetPage },
  { path: 'privacy', component: NotYetPage },
  { path: 'terms', component: NotYetPage },
  { path: '**', component: LandingPage, canActivate: [sessionGuard] },
];
