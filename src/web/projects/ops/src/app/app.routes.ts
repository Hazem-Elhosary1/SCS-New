import { Routes } from '@angular/router';
import { OpsCan } from './access/ops-access.service';
import { guarded } from './access/ops-can';
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
      // The team and its roles (spec 011). Each page stands behind the permission its reads need.
      ...guarded('operators', OpsCan.operatorsView, () =>
        import('./operators/operators.page').then((file) => file.OperatorsPage),
      ),
      ...guarded('operators/roles', OpsCan.operatorsView, () =>
        import('./operators/operators.page').then((file) => file.OperatorsPage),
      ),
      ...guarded('operators/new', OpsCan.operatorsManage, () =>
        import('./operators/operator-form.page').then((file) => file.OperatorFormPage),
      ),
      ...guarded('operators/roles/new', OpsCan.operatorsManageRoles, () =>
        import('./operators/role-form.page').then((file) => file.RoleFormPage),
      ),
      ...guarded('operators/roles/:id/edit', OpsCan.operatorsManageRoles, () =>
        import('./operators/role-form.page').then((file) => file.RoleFormPage),
      ),
      ...guarded('operators/:id', OpsCan.operatorsView, () =>
        import('./operators/operator.page').then((file) => file.OperatorPage),
      ),
      ...guarded('operators/:id/edit', OpsCan.operatorsManage, () =>
        import('./operators/operator-form.page').then((file) => file.OperatorFormPage),
      ),
      {
        path: '**',
        loadComponent: () => import('./home/home.pages').then((file) => file.NotFoundPage),
      },
    ],
  },
];
