import { Injectable, inject } from '@angular/core';
import { AuthService, PlanSource } from 'core';
import { destination } from './destinations';

/**
 * Which destinations the clinic's plan leaves out (spec 014, FR-026): one whose every feature the
 * session's plan withholds — built and not included. An upcoming feature is never withheld, so
 * its destination never shows as not in the plan (ق-٤٩). Read again before a destination of a
 * feature decides, so a plan changed by an operator shows by the next page opened (FR-029).
 */
@Injectable({ providedIn: 'root' })
export class ClinicPlanSource extends PlanSource {
  private readonly auth = inject(AuthService);

  override excludes(destinationKey: string): boolean {
    const features = destination(destinationKey).features ?? [];
    const withheld = this.auth.session()?.plan?.withheld ?? [];
    return features.length > 0 && features.every((feature) => withheld.includes(feature));
  }

  override featureName(destinationKey: string, language: string): string | null {
    const names = this.auth.session()?.plan?.names ?? {};
    const named = (destination(destinationKey).features ?? []).map((feature) => names[feature]).find(Boolean);
    return named ? (language === 'en' ? named.en : named.ar) : null;
  }

  override refresh(): Promise<void> {
    return this.auth.refreshSession();
  }
}
