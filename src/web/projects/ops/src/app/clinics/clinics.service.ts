import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

/** The codes the clinics' screens answer differently (contracts/ops-clinics-api.md). */
export const ClinicCodes = {
  validation: 'VALIDATION_FAILED',
  notFound: 'NOT_FOUND',
  planDisabled: 'PLAN_DISABLED',
  limitReached: 'PLAN_LIMIT_REACHED',
  busy: 'LIMIT_BUSY',
  changedElsewhere: 'CLINIC_CHANGED_ELSEWHERE',
  operatorNotActive: 'OPERATOR_NOT_ACTIVE',
  cooldown: 'RESET_EMAIL_COOLDOWN',
  notSent: 'ACCOUNT_LINK_NOT_SENT',
  accepted: 'INVITATION_ALREADY_ACCEPTED',
} as const;

/** «تجربة» and «نشطة» today; spec 016 adds the others. */
export type ClinicStatusCode = 'trial' | 'active' | string;

export interface ClinicStatus {
  readonly code: ClinicStatusCode;
  readonly ar: string;
  readonly en: string;
  /** The badge's intent: `info` (blue) for a trial, `success` (green) for active. */
  readonly tone: 'info' | 'success' | 'neutral';
}

export interface NamedRef {
  readonly id: string;
  readonly nameAr: string;
  readonly nameEn: string;
}

export interface OperatorRef {
  readonly id: string;
  readonly name: string;
}

export interface ClinicRow {
  readonly id: string;
  readonly slug: string;
  readonly nameAr: string;
  readonly nameEn: string;
  readonly createdAt: string;
  readonly createdBy: OperatorRef | null;
  readonly plan: NamedRef | null;
  readonly status: ClinicStatus;
  /** `trial` with its days; null until spec 018 gives monthly and yearly. */
  readonly length: { readonly code: string; readonly days: number | null } | null;
  /** The trial's end while in trial. */
  readonly endsAt: string | null;
  readonly ownerName: string | null;
  readonly ownerEmail: string | null;
  readonly ownerMobile: string | null;
}

export interface ClinicsPage {
  readonly items: readonly ClinicRow[];
  readonly total: number;
  readonly page: number;
  readonly size: number;
  readonly cards: {
    readonly total: number;
    readonly newThisMonth: number;
    readonly active: number;
    readonly activeShare: number;
    readonly trial: number;
    readonly trialEndingSoon: boolean;
    readonly needsFollowUp: { readonly overdue: number; readonly readOnly: number; readonly suspended: number };
  };
  readonly facets: {
    readonly statuses: readonly string[];
    readonly plans: readonly NamedRef[];
    readonly lengths: readonly string[];
    readonly endsWithin: readonly number[];
  };
  readonly can: { readonly add: boolean; readonly edit: boolean };
}

export interface ClinicsQuery {
  readonly search?: string;
  readonly statuses?: readonly string[];
  readonly plan?: string;
  readonly length?: string;
  readonly endsWithin?: number;
  readonly sort?: 'new' | 'ends-asc' | 'ends-desc';
  readonly page?: number;
  readonly size?: number;
}

export interface ClinicFormOptions {
  readonly plans: readonly { readonly id: string; readonly nameAr: string; readonly nameEn: string; readonly monthlyEgp: number }[];
  readonly specialties: readonly string[];
}

export type InvitationState = 'live' | 'lapsed' | 'none';

export interface ClinicOwner {
  readonly name: string | null;
  readonly email: string;
  readonly mobile: string | null;
  readonly state: 'invited' | 'active' | 'disabled';
  readonly lastSignInAt: string | null;
  /** Only while the owner is invited. */
  readonly invitation: { readonly state: InvitationState; readonly at: string | null } | null;
}

export interface ClinicDetail {
  readonly id: string;
  readonly slug: string | null;
  readonly nameAr: string;
  readonly nameEn: string;
  readonly createdAt: string;
  readonly createdBy: OperatorRef | null;
  readonly version: string;
  readonly status: ClinicStatus;
  readonly plan: NamedRef | null;
  readonly billingCycle: string | null;
  readonly nextRenewal: string | null;
  readonly address: string | null;
  readonly taxNumber: string | null;
  readonly specialty: string | null;
  readonly defaultLanguage: 'ar' | 'en';
  readonly signInAddress: string;
  readonly owner: ClinicOwner | null;
  readonly accountManager: OperatorRef | null;
  /** The active operators, for «تعيين»; empty for whoever may not assign. */
  readonly managers: readonly OperatorRef[];
  readonly doctors: number;
  readonly branches: number;
  readonly can: { readonly edit: boolean };
}

export interface NewClinicBody {
  readonly nameAr: string;
  readonly nameEn: string;
  readonly ownerName: string;
  readonly email: string;
  readonly mobile: string;
  readonly address: string;
  readonly planId: string | null;
  readonly taxNumber: string;
  readonly specialty: string | null;
}

export interface EditClinicBody {
  readonly version: string;
  readonly nameAr: string;
  readonly nameEn: string;
  readonly ownerName: string;
  readonly email: string;
  readonly mobile: string;
  readonly address: string;
  /** Sent only when touched: `""` removes it (FR-022). */
  readonly taxNumber?: string;
  readonly taxNumberSent: boolean;
  readonly specialty: string | null;
  readonly defaultLanguage: string;
  readonly note: string;
}

export interface ClinicSettings {
  readonly defaultLanguage: 'ar' | 'en';
  readonly timeZone: string;
  readonly staffAddress: string;
  readonly can: { readonly edit: boolean };
}

const CLINICS = '/v1/ops/clinics';

/**
 * The clinics of the operators console (spec 015; contracts/ops-clinics-api.md). Nothing here
 * keeps anything: each page asks when it opens and after each change.
 */
@Injectable({ providedIn: 'root' })
export class ClinicsService {
  private readonly http = inject(HttpClient);

  list(query: ClinicsQuery = {}): Promise<ClinicsPage> {
    let params = new HttpParams();
    const set = (name: string, value: string | number | undefined) => {
      if (value !== undefined && value !== '') {
        params = params.set(name, String(value));
      }
    };
    set('search', query.search?.trim());
    for (const status of query.statuses ?? []) {
      params = params.append('status', status);
    }
    set('plan', query.plan);
    set('length', query.length);
    set('endsWithin', query.endsWithin);
    set('sort', query.sort === 'new' ? undefined : query.sort);
    set('page', query.page);
    set('size', query.size);
    return firstValueFrom(this.http.get<ClinicsPage>(CLINICS, { params }));
  }

  form(): Promise<ClinicFormOptions> {
    return firstValueFrom(this.http.get<ClinicFormOptions>(`${CLINICS}/form`));
  }

  add(body: NewClinicBody): Promise<{ id: string; slug: string }> {
    return firstValueFrom(this.http.post<{ id: string; slug: string }>(CLINICS, body));
  }

  read(id: string): Promise<ClinicDetail> {
    return firstValueFrom(this.http.get<ClinicDetail>(`${CLINICS}/${encodeURIComponent(id)}`));
  }

  async edit(id: string, body: EditClinicBody): Promise<void> {
    await firstValueFrom(this.http.put(`${CLINICS}/${encodeURIComponent(id)}`, body));
  }

  assign(id: string, version: string, operatorId: string | null): Promise<{ accountManager: OperatorRef | null; version: string }> {
    return firstValueFrom(
      this.http.put<{ accountManager: OperatorRef | null; version: string }>(
        `${CLINICS}/${encodeURIComponent(id)}/account-manager`,
        { version, operatorId },
      ),
    );
  }

  invite(id: string): Promise<{ expiresAt: string }> {
    return firstValueFrom(this.http.post<{ expiresAt: string }>(`${CLINICS}/${encodeURIComponent(id)}/invitation`, null));
  }

  settings(id: string): Promise<ClinicSettings> {
    return firstValueFrom(this.http.get<ClinicSettings>(`${CLINICS}/${encodeURIComponent(id)}/settings`));
  }
}
