import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

/** The seven groups, in the catalogue's order (spec 012, FR-009). */
export const FEATURE_GROUPS = [
  'core',
  'channels',
  'reports',
  'money',
  'clinical',
  'ai',
  'growth',
] as const;

/** The fifteen specialties, in their order (SH-013, FR-013). */
export const SPECIALTIES = [
  'dental',
  'orthopedics',
  'neurology',
  'pediatrics',
  'internal_medicine',
  'general_surgery',
  'dermatology',
  'obgyn',
  'ophthalmology',
  'ent',
  'urology',
  'cardiology',
  'psychiatry',
  'nutrition',
  'physiotherapy',
] as const;

/** The one feature the clinic's specialty decides: its type reads «يتبع التخصص». */
export const SPECIALTY_DECIDED = 'specialty_exam_modules';

export type FeatureGroup = (typeof FEATURE_GROUPS)[number];
export type FeatureKind = 'switch' | 'limit';
export type FeatureStatus = 'built' | 'upcoming';

export interface FeatureRow {
  readonly key: string;
  readonly nameAr: string;
  readonly nameEn: string;
  readonly descriptionAr: string | null;
  readonly descriptionEn: string | null;
  readonly group: FeatureGroup;
  readonly kind: FeatureKind;
  readonly unitAr: string | null;
  readonly unitEn: string | null;
  readonly baseline: number | null;
  readonly specialty: string | null;
  readonly status: FeatureStatus;
  readonly isSold: boolean;
  readonly isRetired: boolean;
  readonly sortOrder: number;
  readonly monthly: boolean;
  /** How many plans include it: 0 until plans exist (spec 013). */
  readonly plans: number;
  readonly version: number;
}

export interface FeaturesView {
  readonly items: readonly FeatureRow[];
  readonly cards: {
    readonly total: number;
    readonly groups: number;
    readonly built: number;
    readonly upcoming: number;
    readonly soldInPlans: number;
    readonly plans: number;
  };
  readonly can: { readonly manage: boolean };
}

export interface PlanUse {
  readonly id: string;
  readonly nameAr: string;
  readonly nameEn: string;
  /** As the plans say it (spec 013). */
  readonly state: 'active' | 'disabled';
  readonly clinics: number;
}

export interface WhereUsed {
  readonly items: readonly PlanUse[];
  readonly total: number;
}

/** What the form sends. The key only when adding; the version only when editing. */
export interface FeatureBody {
  readonly key?: string;
  readonly nameAr: string;
  readonly nameEn: string;
  readonly descriptionAr: string | null;
  readonly descriptionEn: string | null;
  readonly group: string;
  readonly kind: FeatureKind;
  readonly unitAr: string | null;
  readonly unitEn: string | null;
  readonly baseline: number | null;
  readonly specialty: string | null;
  readonly status: FeatureStatus;
  readonly isSold: boolean;
  readonly version?: number;
}

export const FeatureCodes = {
  validation: 'VALIDATION_FAILED',
  notFound: 'NOT_FOUND',
  keyTaken: 'FEATURE_KEY_TAKEN',
  unitsRequired: 'FEATURE_UNITS_REQUIRED',
  switchHasLimit: 'FEATURE_SWITCH_HAS_LIMIT',
  retiredMustBeSold: 'FEATURE_RETIRED_MUST_BE_SOLD',
  stateChanged: 'FEATURE_STATE_CHANGED',
  changedElsewhere: 'FEATURE_CHANGED_ELSEWHERE',
} as const;

const FEATURES = '/v1/ops/features';

/**
 * The feature catalogue's addresses (spec 012; contracts/features-api.md). The whole catalogue
 * is one answer: search, filter and paging are the page's. Nothing here keeps anything.
 */
@Injectable({ providedIn: 'root' })
export class FeaturesService {
  private readonly http = inject(HttpClient);

  list(): Promise<FeaturesView> {
    return firstValueFrom(this.http.get<FeaturesView>(FEATURES));
  }

  read(key: string): Promise<FeatureRow> {
    return firstValueFrom(this.http.get<FeatureRow>(`${FEATURES}/${encodeURIComponent(key)}`));
  }

  whereUsed(key: string): Promise<WhereUsed> {
    return firstValueFrom(this.http.get<WhereUsed>(`${FEATURES}/${encodeURIComponent(key)}/plans`));
  }

  async add(body: FeatureBody): Promise<string> {
    return (await firstValueFrom(this.http.post<{ key: string }>(FEATURES, body))).key;
  }

  async edit(key: string, body: FeatureBody): Promise<void> {
    await firstValueFrom(this.http.put(`${FEATURES}/${encodeURIComponent(key)}`, body));
  }

  async retire(key: string): Promise<void> {
    await firstValueFrom(this.http.post(`${FEATURES}/${encodeURIComponent(key)}/retire`, null));
  }

  async resume(key: string): Promise<void> {
    await firstValueFrom(this.http.post(`${FEATURES}/${encodeURIComponent(key)}/resume`, null));
  }
}
