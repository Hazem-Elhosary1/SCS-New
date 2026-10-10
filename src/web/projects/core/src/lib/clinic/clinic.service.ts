import { HttpClient } from '@angular/common/http';
import { DOCUMENT, Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ScsProblem, ScsProblemError } from '../http/problem';

/** Answer of `GET /v1/clinic` (specs/002-clinic-tenancy-isolation/contracts/tenancy-api.openapi.yaml). */
export interface ClinicSummary {
  readonly shortName: string;
  readonly nameAr: string;
  readonly nameEn: string;
  readonly state: string;
  readonly renamed: boolean;
}

export type ClinicStatus =
  | { readonly state: 'loading' }
  | { readonly state: 'ready'; readonly clinic: ClinicSummary }
  /** The address names no clinic a patient may open. Shown identically for every such address. */
  | { readonly state: 'not-available' }
  /** The platform could not be asked; not a statement about the clinic. */
  | { readonly state: 'failed'; readonly problem: ScsProblem };

export const CLINIC_HEADER = 'X-Scs-Clinic';
export const CLINIC_NOT_AVAILABLE = 'CLINIC_NOT_AVAILABLE';

const SHORT_NAME = /^[a-z][a-z0-9]*(-[a-z0-9]+)*$/;

/**
 * The clinic's short name in a patient address: the first part of the path, in lower case.
 * Null when there is none or when it could not be anyone's name (wrong characters, too short,
 * too long), in which case the platform is not asked at all.
 */
export function clinicFromPath(pathname: string): string | null {
  const first = pathname.split('/').find((part) => part.length > 0);
  if (first === undefined) {
    return null;
  }

  let decoded: string;
  try {
    decoded = decodeURIComponent(first);
  } catch {
    return null;
  }

  const candidate = decoded.trim().toLowerCase();
  return candidate.length >= 3 && candidate.length <= 40 && SHORT_NAME.test(candidate)
    ? candidate
    : null;
}

/**
 * Which clinic the patient site is showing (spec 002, User Story 3). The clinic is named by
 * the address; the platform says whether such a clinic exists and what it is called.
 */
@Injectable({ providedIn: 'root' })
export class ClinicService {
  private readonly http = inject(HttpClient);
  private readonly document = inject(DOCUMENT);
  private readonly current = signal<ClinicStatus>({ state: 'loading' });

  readonly status = this.current.asReadonly();

  /** The short name in the current address, or null. Read fresh each time. */
  shortName(): string | null {
    return clinicFromPath(this.document.location?.pathname ?? '/');
  }

  async load(): Promise<ClinicStatus> {
    if (this.shortName() === null) {
      this.current.set({ state: 'not-available' });
      return this.current();
    }

    this.current.set({ state: 'loading' });
    try {
      const clinic = await firstValueFrom(this.http.get<ClinicSummary>('/v1/clinic'));
      this.current.set({ state: 'ready', clinic });
    } catch (error) {
      if (!(error instanceof ScsProblemError)) {
        throw error;
      }
      this.current.set(
        error.problem.code === CLINIC_NOT_AVAILABLE
          ? { state: 'not-available' }
          : { state: 'failed', problem: error.problem },
      );
    }
    return this.current();
  }
}
