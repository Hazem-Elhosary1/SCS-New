import { Directive, TemplateRef, ViewContainerRef, effect, inject, input } from '@angular/core';
import { CanMatchFn, Route } from '@angular/router';
import { OpsAuthService } from '../auth/ops-auth.service';
import { OpsAccess } from './ops-access.service';

/**
 * A page opens only for an operator whose role holds its permission (spec 011, FR-007). The
 * session is read again first, so a role changed a moment ago is the one that decides. Whoever
 * may not open it falls through to the route after it — the "you can't open this page" state —
 * at the same address.
 */
export function opsCan(code: string): CanMatchFn {
  return async () => {
    const auth = inject(OpsAuthService);
    const access = inject(OpsAccess);
    // A route is matched before the frame's own guard has run: the session is read here first.
    if (!(await auth.load())) {
      return false;
    }
    await auth.refresh();
    return access.can(code);
  };
}

/**
 * A route behind a permission, and what stands at its address for anyone without it. The page
 * itself is not loaded for them.
 */
export function guarded(path: string, code: string, load: NonNullable<Route['loadComponent']>): Route[] {
  return [
    { path, canMatch: [opsCan(code)], loadComponent: load },
    {
      path,
      loadComponent: () => import('../home/home.pages').then((file) => file.NoAccessPage),
    },
  ];
}

/**
 * Draws its content only for whoever holds the permission: `*appCan="'operators.manage'"`. A
 * control that is not drawn is not a guard — the platform refuses the request all the same.
 */
@Directive({ selector: '[appCan]' })
export class OpsCanDirective {
  private readonly template = inject<TemplateRef<unknown>>(TemplateRef);
  private readonly view = inject(ViewContainerRef);
  private readonly access = inject(OpsAccess);

  readonly appCan = input.required<string>();

  private shown = false;

  constructor() {
    effect(() => {
      const may = this.access.can(this.appCan());
      if (may && !this.shown) {
        this.view.createEmbeddedView(this.template);
        this.shown = true;
      } else if (!may && this.shown) {
        this.view.clear();
        this.shown = false;
      }
    });
  }
}
