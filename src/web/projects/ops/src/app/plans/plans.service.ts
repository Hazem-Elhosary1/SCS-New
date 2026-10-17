import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { FeatureGroup, FeatureKind, FeatureStatus } from './features.service';

/** The badges a plan may carry for clinics (spec 013, ق-٤٥); at most one plan holds one. */
export const PLAN_BADGES = ['best', 'most_popular', 'best_selling'] as const;

export type PlanBadge = (typeof PLAN_BADGES)[number];
export type PlanState = 'active' | 'disabled';

export const PlanCodes = {
  validation: 'VALIDATION_FAILED',
  notFound: 'NOT_FOUND',
  featureUnknown: 'PLAN_FEATURE_UNKNOWN',
  switchHasLimit: 'PLAN_SWITCH_HAS_LIMIT',
  featureNotSold: 'PLAN_FEATURE_NOT_SOLD',
  badgeOnDisabled: 'PLAN_BADGE_ON_DISABLED',
  changedElsewhere: 'PLAN_CHANGED_ELSEWHERE',
  codeTaken: 'PLAN_CODE_TAKEN',
} as const;

/** One plan as the list reads it (contracts/plans-api.md §1). */
export interface PlanRow {
  readonly id: string;
  readonly code: string;
  readonly nameAr: string;
  readonly nameEn: string;
  readonly monthlyEgp: number;
  /** 0 means no annual price: a year is twelve monthly prices. */
  readonly annualEgp: number;
  readonly monthlyUsd: number;
  readonly annualUsd: number;
  readonly badge: PlanBadge | null;
  readonly state: PlanState;
  /** The users the plan allows: null for no limit. */
  readonly users: { readonly limit: number | null };
  readonly clinics: number;
  readonly createdAt: string;
  readonly updatedAt: string;
  readonly version: number;
}

export interface PlansView {
  readonly items: readonly PlanRow[];
  readonly cards: {
    readonly total: number;
    readonly disabled: number;
    readonly clinics: number;
    readonly clinicsNewThisMonth: number;
    readonly mostWanted: {
      readonly id: string;
      readonly nameAr: string;
      readonly nameEn: string;
      readonly clinics: number;
    } | null;
    /** Null while no clinic is on a plan (spec 015, 016). */
    readonly revenue: { readonly now: number; readonly lastMonthEnd: number | null } | null;
  };
  readonly can: { readonly manage: boolean };
}

export interface PlanClinic {
  readonly id: string;
  readonly nameAr: string;
  readonly nameEn: string;
  readonly stateAr: string;
  readonly stateEn: string;
}

/** One plan in full, for its detail and its form; its clinics' count is in `subscriptions`. */
export interface PlanDetail extends Omit<PlanRow, 'clinics'> {
  readonly description: string;
  readonly disabledReason: string | null;
  readonly disabledAt: string | null;
  /** What it includes: a limit's number, or null for no limit (and for a switch). */
  readonly features: readonly { readonly key: string; readonly limit: number | null }[];
  /** How many catalogue features offered to it it does not include. */
  readonly excluded: number;
  readonly subscriptions: {
    readonly clinics: number;
    readonly monthlyRevenue: number;
    readonly newThisMonth: number;
    readonly onTrial: number;
  };
  /** The clinics on it, newest first (spec 015, 016); empty until then. */
  readonly clinics: readonly PlanClinic[];
}

/** A feature the plan form offers (contracts/plans-api.md §1, the grid). */
export interface GridFeature {
  readonly key: string;
  readonly nameAr: string;
  readonly nameEn: string;
  readonly group: FeatureGroup;
  readonly kind: FeatureKind;
  readonly unitAr: string | null;
  readonly unitEn: string | null;
  readonly status: FeatureStatus;
  /** Only an edited plan's kept rows can be retired or not sold. */
  readonly retired: boolean;
  readonly sold: boolean;
}

export interface PlanGrid {
  readonly items: readonly GridFeature[];
  readonly badgeHolder: {
    readonly id: string;
    readonly nameAr: string;
    readonly nameEn: string;
    readonly badge: PlanBadge;
  } | null;
}

/** What the form sends. The version only when editing; the code never. */
export interface PlanBody {
  readonly nameAr: string;
  readonly nameEn: string;
  readonly description: string;
  readonly monthlyEgp: number;
  readonly annualEgp: number;
  readonly monthlyUsd: number;
  readonly annualUsd: number;
  readonly badge: PlanBadge | null;
  readonly note: string | null;
  readonly features: readonly { readonly key: string; readonly limit: number | null }[];
  readonly version?: number;
}

const PLANS = '/v1/ops/plans';

/**
 * The plans' addresses (spec 013; contracts/plans-api.md). The whole list is one answer: search,
 * filter, paging and the order of equal prices are the page's (research R13). Nothing here keeps
 * anything.
 */
@Injectable({ providedIn: 'root' })
export class PlansService {
  private readonly http = inject(HttpClient);

  list(): Promise<PlansView> {
    return firstValueFrom(this.http.get<PlansView>(PLANS));
  }

  read(id: string): Promise<PlanDetail> {
    return firstValueFrom(this.http.get<PlanDetail>(`${PLANS}/${encodeURIComponent(id)}`));
  }

  /** What the add form offers; with an id, what that plan's edit form offers. */
  grid(id: string | null = null): Promise<PlanGrid> {
    const address = id === null ? `${PLANS}/grid` : `${PLANS}/${encodeURIComponent(id)}/grid`;
    return firstValueFrom(this.http.get<PlanGrid>(address));
  }

  add(body: PlanBody): Promise<{ id: string; code: string }> {
    return firstValueFrom(this.http.post<{ id: string; code: string }>(PLANS, body));
  }

  async edit(id: string, body: PlanBody): Promise<void> {
    await firstValueFrom(this.http.put(`${PLANS}/${encodeURIComponent(id)}`, body));
  }

  async disable(id: string, reason: string | null): Promise<void> {
    await firstValueFrom(this.http.post(`${PLANS}/${encodeURIComponent(id)}/disable`, { reason }));
  }

  async enable(id: string): Promise<void> {
    await firstValueFrom(this.http.post(`${PLANS}/${encodeURIComponent(id)}/enable`, null));
  }
}
