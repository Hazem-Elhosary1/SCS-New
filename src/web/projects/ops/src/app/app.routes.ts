import { Routes } from '@angular/router';
import { signedInOnly, signedOutOnly } from './auth/ops-auth.guard';

/**
 * The operators console (spec 010). The screens before sign-in stand alone; everything after
 * it sits in the frame. A later spec adds its page under the frame and its line in
 * `shell/sections.ts`.
 */
export const routes: Routes = [
  {
    path: 'sign-in',
    canActivate: [signedOutOnly],
    loadComponent: () => import('./sign-in/sign-in.page').then((file) => file.SignInPage),
  },
  {
    path: 'sign-in/code',
    canActivate: [signedOutOnly],
    loadComponent: () => import('./sign-in/code.page').then((file) => file.CodePage),
  },
  {
    path: 'forgot-password',
    canActivate: [signedOutOnly],
    loadComponent: () => import('./password/password.pages').then((file) => file.ForgotPage),
  },
  {
    path: 'password/reset',
    loadComponent: () => import('./password/password.pages').then((file) => file.SetPasswordPage),
  },
  {
    path: 'password/first',
    loadComponent: () => import('./password/password.pages').then((file) => file.SetPasswordPage),
  },
  {
    path: 'privacy',
    loadComponent: () => import('./home/home.pages').then((file) => file.NotYetPage),
  },
  {
    path: '',
    canActivate: [signedInOnly],
    loadComponent: () => import('./shell/ops-shell.component').then((file) => file.OpsShellComponent),
    children: [
      {
        path: '',
        pathMatch: 'full',
        loadComponent: () => import('./home/home.pages').then((file) => file.HomePage),
      },
      {
        path: 'profile',
        loadComponent: () => import('./profile/profile.page').then((file) => file.ProfilePage),
      },
      {
        path: '**',
        loadComponent: () => import('./home/home.pages').then((file) => file.NotFoundPage),
      },
    ],
  },
];
