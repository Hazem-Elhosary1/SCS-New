import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { ScsProblemError } from 'core';
import { firstValueFrom } from 'rxjs';
import { OpsRoleRef } from '../access/ops-access.service';

export type OperatorState = 'active' | 'invited' | 'suspended';

export interface OperatorRow {
  readonly id: string;
  readonly fullName: string;
  readonly email: string;
  readonly role: OpsRoleRef;
  readonly lastSignInAtUtc: string | null;
  readonly state: OperatorState;
  readonly twoStep: boolean;
}

export interface OperatorsPageView {
  readonly items: readonly OperatorRow[];
  readonly total: number;
  readonly page: number;
  readonly pageSize: number;
  readonly cards: {
    readonly total: number;
    readonly active: number;
    readonly activatedThisMonth: number;
    readonly invited: number;
    readonly twoStepOn: number;
  };
  readonly filters: { readonly states: readonly OperatorState[]; readonly roles: readonly OpsRoleRef[] };
}

/** One line of an operator's recent activity; the console writes its sentence. */
export interface ActivityLine {
  readonly kind: string;
  readonly name: string | null;
  readonly role: string | null;
  readonly roleKey: string | null;
  readonly device: string | null;
  readonly atUtc: string;
}

export interface OperatorDetail extends OperatorRow {
  readonly mobile: string | null;
  readonly department: string | null;
  readonly jobTitle: string | null;
  readonly language: 'ar' | 'en';
  readonly createdAtUtc: string;
  readonly invitedBy: { readonly id: string; readonly name: string } | null;
  readonly firstOperator: boolean;
  readonly locked: boolean;
  readonly permissions: readonly string[];
  readonly performance: readonly { readonly key: string; readonly value: number }[];
  readonly activity: readonly ActivityLine[];
  readonly version: string;
  readonly can: {
    readonly edit: boolean;
    readonly suspend: boolean;
    readonly reactivate: boolean;
    readonly sendLink: boolean;
    readonly unlock: boolean;
  };
}

/** What an operator's form sends. The email is taken only when adding. */
export interface OperatorBody {
  readonly fullName: string;
  readonly email?: string;
  readonly roleId: string | null;
  readonly mobile: string | null;
  readonly department: string | null;
  readonly jobTitle: string | null;
  readonly language: string;
  readonly twoStep: boolean;
  readonly sendInvitation?: boolean;
  readonly note?: string | null;
  readonly version?: string;
}

export interface RoleRow {
  readonly id: string;
  readonly name: string;
  readonly systemKey: string | null;
  readonly description: string | null;
  readonly operators: number;
  readonly inUse: boolean;
  readonly permissions: number;
  readonly can: { readonly edit: boolean; readonly delete: boolean };
}

export interface RolesListView {
  readonly items: readonly RoleRow[];
  readonly cards: { readonly total: number; readonly inUse: number; readonly custom: number; readonly system: number };
}

export interface RoleView {
  readonly id: string;
  readonly name: string;
  readonly description: string | null;
  readonly systemKey: string | null;
  readonly codes: readonly string[];
  readonly version: string;
  readonly readOnlyReason: 'system' | 'own-role' | null;
  /** What a new role based on this one starts with, for whoever asks, and what was left out. */
  readonly copy: { readonly codes: readonly string[]; readonly left: readonly string[] };
}

export interface RoleOption extends OpsRoleRef {
  readonly id: string;
  readonly codes: readonly string[];
  readonly grantable: boolean;
  readonly reason: 'holds-more' | 'super-admin-only' | null;
}

export interface CatalogueGroup {
  readonly key: string;
  readonly items: readonly { readonly code: string; readonly needs: string | null; readonly mayGrant: boolean }[];
}

export interface RoleBody {
  readonly name: string;
  readonly description: string | null;
  readonly codes: readonly string[];
  readonly basedOnId?: string | null;
  readonly note?: string | null;
  readonly version?: string;
}

export const TeamCodes = {
  validation: 'VALIDATION_FAILED',
  notFound: 'NOT_FOUND',
  emailTaken: 'OPS_EMAIL_TAKEN',
  roleNotGrantable: 'OPS_ROLE_NOT_GRANTABLE',
  ownRole: 'OPS_OWN_ROLE',
  lastSuperAdmin: 'OPS_LAST_SUPER_ADMIN',
  stateChanged: 'OPS_STATE_CHANGED',
  linkTooSoon: 'OPS_LINK_TOO_SOON',
  changedElsewhere: 'OPS_CHANGED_ELSEWHERE',
  roleNameTaken: 'OPS_ROLE_NAME_TAKEN',
  permissionsNotHeld: 'OPS_PERMISSIONS_NOT_HELD',
  ownRoleEdit: 'OPS_OWN_ROLE_EDIT',
  systemRole: 'OPS_SYSTEM_ROLE',
  roleInUse: 'OPS_ROLE_IN_USE',
} as const;

/** The code of a refusal; empty for anything that is not one (no connection, a fault). */
export function codeOf(error: unknown): string {
  return error instanceof ScsProblemError ? error.problem.code : '';
}

/** Which fields a "validation failed" names, each with its first reason. */
export function fieldErrors(error: unknown): Record<string, string> {
  if (!(error instanceof ScsProblemError)) {
    return {};
  }
  const errors = (error.problem as unknown as { errors?: Record<string, string[]> }).errors ?? {};
  return Object.fromEntries(Object.entries(errors).map(([field, codes]) => [field, codes[0] ?? '']));
}

const OPERATORS = '/v1/ops/operators';
const ROLES = '/v1/ops/roles';

/**
 * The addresses of the team and its roles (spec 011; contracts/operators-roles-api.md). Nothing
 * here keeps anything: each page asks when it opens and after each change.
 */
@Injectable({ providedIn: 'root' })
export class OperatorsService {
  private readonly http = inject(HttpClient);

  list(query: { search: string; state: string; roleId: string; page: number }): Promise<OperatorsPageView> {
    let params = new HttpParams().set('page', query.page);
    for (const name of ['search', 'state', 'roleId'] as const) {
      if (query[name] !== '') {
        params = params.set(name, query[name]);
      }
    }
    return firstValueFrom(this.http.get<OperatorsPageView>(OPERATORS, { params }));
  }

  read(id: string): Promise<OperatorDetail> {
    return firstValueFrom(this.http.get<OperatorDetail>(`${OPERATORS}/${id}`));
  }

  async invite(body: OperatorBody): Promise<string> {
    return (await firstValueFrom(this.http.post<{ id: string }>(OPERATORS, body))).id;
  }

  async edit(id: string, body: OperatorBody): Promise<void> {
    await firstValueFrom(this.http.put(`${OPERATORS}/${id}`, body));
  }

  async suspend(id: string, reason: string): Promise<void> {
    await firstValueFrom(this.http.post(`${OPERATORS}/${id}/suspend`, { reason: reason.trim() || null }));
  }

  async reactivate(id: string): Promise<void> {
    await firstValueFrom(this.http.post(`${OPERATORS}/${id}/reactivate`, null));
  }

  async sendLink(id: string): Promise<void> {
    await firstValueFrom(this.http.post(`${OPERATORS}/${id}/password-link`, null));
  }

  async unlock(id: string): Promise<void> {
    await firstValueFrom(this.http.post(`${OPERATORS}/${id}/unlock`, null));
  }

  roles(): Promise<RolesListView> {
    return firstValueFrom(this.http.get<RolesListView>(ROLES));
  }

  role(id: string): Promise<RoleView> {
    return firstValueFrom(this.http.get<RoleView>(`${ROLES}/${id}`));
  }

  async roleOptions(): Promise<readonly RoleOption[]> {
    return (await firstValueFrom(this.http.get<{ items: RoleOption[] }>(`${ROLES}/options`))).items;
  }

  async catalogue(): Promise<readonly CatalogueGroup[]> {
    return (await firstValueFrom(this.http.get<{ groups: CatalogueGroup[] }>('/v1/ops/permissions'))).groups;
  }

  async createRole(body: RoleBody): Promise<string> {
    return (await firstValueFrom(this.http.post<{ id: string }>(ROLES, body))).id;
  }

  async editRole(id: string, body: RoleBody): Promise<void> {
    await firstValueFrom(this.http.put(`${ROLES}/${id}`, body));
  }

  async deleteRole(id: string, note: string): Promise<void> {
    await firstValueFrom(this.http.post(`${ROLES}/${id}/delete`, { note: note.trim() || null }));
  }
}

