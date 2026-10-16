import { Injectable, computed, inject } from '@angular/core';
import { LanguageService } from 'core';
import { OpsAuthService } from '../auth/ops-auth.service';
import catalogue from './operator-permissions.json';

/** A role as the platform names it: a system role by its key, a custom one by its name as written. */
export interface OpsRoleRef {
  readonly id: string | null;
  readonly name: string;
  readonly systemKey: string | null;
}

export interface OpsPermission {
  readonly code: string;
  readonly group: string;
  readonly nameAr: string;
  readonly nameEn: string;
  readonly needs: string | null;
}

export interface OpsPermissionGroup {
  readonly key: string;
  readonly nameAr: string;
  readonly nameEn: string;
}

/** The operators' catalogue, generated from the platform's own (`Scs.Migrator ops-catalogue`). */
export const OPS_PERMISSIONS: readonly OpsPermission[] = catalogue.permissions;
export const OPS_PERMISSION_GROUPS: readonly OpsPermissionGroup[] = catalogue.groups;

export const OpsCan = {
  operatorsView: 'operators.view',
  operatorsManage: 'operators.manage',
  operatorsManageRoles: 'operators.manageRoles',
  billingView: 'billing.view',
  billingManagePlans: 'billing.managePlans',
} as const;

export const SUPER_ADMIN = 'super-admin';

const BY_CODE = new Map(OPS_PERMISSIONS.map((permission) => [permission.code, permission]));
const ORDER = new Map(OPS_PERMISSIONS.map((permission, index) => [permission.code, index]));

/** Codes in the order the role editor draws them. */
export function inCatalogueOrder(codes: readonly string[]): string[] {
  return [...new Set(codes)].sort((a, b) => (ORDER.get(a) ?? 999) - (ORDER.get(b) ?? 999));
}

/**
 * What the signed-in operator may see and do in the console (spec 011, FR-006 to FR-009;
 * research R14): their role and the codes it holds, as the session read gave them. It decides
 * what is drawn — a section, a button — and nothing more: the platform judges every request.
 */
@Injectable({ providedIn: 'root' })
export class OpsAccess {
  private readonly auth = inject(OpsAuthService);
  private readonly language = inject(LanguageService);

  private readonly codes = computed(() => new Set(this.auth.session()?.permissions ?? []));

  readonly role = computed<OpsRoleRef | null>(() => this.auth.session()?.operator.role ?? null);

  readonly isSuperAdmin = computed(() => this.role()?.systemKey === SUPER_ADMIN);

  /** The role's name as the top bar writes it: a system role in the page's language. */
  readonly roleName = computed(() => {
    const role = this.role();
    return role === null ? (this.auth.session()?.operator.roleName ?? '') : this.nameOfRole(role);
  });

  can(code: string): boolean {
    return this.codes().has(code);
  }

  /** A system role in the page's language; a custom one as written. */
  nameOfRole(role: { readonly name: string; readonly systemKey: string | null }): string {
    const system = catalogue.systemRoles.find((known) => known.key === role.systemKey);
    if (system === undefined) {
      return role.name;
    }
    return this.language.language() === 'en' ? system.nameEn : system.nameAr;
  }

  nameOfPermission(code: string): string {
    const permission = BY_CODE.get(code);
    if (permission === undefined) {
      return code;
    }
    return this.language.language() === 'en' ? permission.nameEn : permission.nameAr;
  }

  nameOfGroup(key: string): string {
    const group = OPS_PERMISSION_GROUPS.find((known) => known.key === key);
    if (group === undefined) {
      return key;
    }
    return this.language.language() === 'en' ? group.nameEn : group.nameAr;
  }
}
