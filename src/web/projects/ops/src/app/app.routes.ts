import { Routes } from '@angular/router';
import { OpsCan } from './access/ops-access.service';
import { guarded } from './access/ops-can';
import { signedInOnly, signedOutOnly } from './auth/ops-auth.guard';
import { galleryRoutes } from './gallery/gallery.routes';

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
  // The design components' gallery: development only, empty in production (spec 095).
  ...galleryRoutes,
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
      // The clinics (spec 015). `clinics/new` before `clinics/:id`; the detail's two tabs are two addresses of one page.
      ...guarded('clinics', OpsCan.clinicsView, () =>
        import('./clinics/clinics-list.page').then((file) => file.ClinicsListPage),
      ),
      ...guarded('clinics/new', OpsCan.clinicsEdit, () =>
        import('./clinics/clinic-form.page').then((file) => file.ClinicFormPage),
      ),
      ...guarded('clinics/:id', OpsCan.clinicsView, () =>
        import('./clinics/clinic-detail.page').then((file) => file.ClinicDetailPage),
      ),
      ...guarded('clinics/:id/settings', OpsCan.clinicsView, () =>
        import('./clinics/clinic-detail.page').then((file) => file.ClinicDetailPage),
      ),
      ...guarded('clinics/:id/edit', OpsCan.clinicsEdit, () =>
        import('./clinics/clinic-form.page').then((file) => file.ClinicFormPage),
      ),
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
      // The feature catalogue (spec 012): read with «عرض الباقات والفواتير», written with «إدارة الباقات والاشتراكات».
      ...guarded('plans/features', OpsCan.billingView, () =>
        import('./plans/feature-catalogue.page').then((file) => file.FeatureCataloguePage),
      ),
      ...guarded('plans/features/new', OpsCan.billingManagePlans, () =>
        import('./plans/feature-form.page').then((file) => file.FeatureFormPage),
      ),
      ...guarded('plans/features/:key/edit', OpsCan.billingManagePlans, () =>
        import('./plans/feature-form.page').then((file) => file.FeatureFormPage),
      ),
      // The plans (spec 013), after the catalogue's addresses so that `plans/:id` never takes them.
      ...guarded('plans', OpsCan.billingView, () =>
        import('./plans/plans-list.page').then((file) => file.PlansListPage),
      ),
      ...guarded('plans/new', OpsCan.billingManagePlans, () =>
        import('./plans/plan-form.page').then((file) => file.PlanFormPage),
      ),
      ...guarded('plans/:id', OpsCan.billingView, () =>
        import('./plans/plan-detail.page').then((file) => file.PlanDetailPage),
      ),
      ...guarded('plans/:id/edit', OpsCan.billingManagePlans, () =>
        import('./plans/plan-form.page').then((file) => file.PlanFormPage),
      ),
      {
        path: '**',
        loadComponent: () => import('./home/home.pages').then((file) => file.NotFoundPage),
      },
    ],
  },
];
