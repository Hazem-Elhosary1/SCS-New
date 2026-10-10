import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

/** The support hours and channels shown in the help window (spec 003, FR-050). */
export interface SupportChannels {
  readonly hoursAr: string;
  readonly hoursEn: string;
  readonly hotline: string;
  readonly whatsapp: string;
  readonly email: string;
}

/** Reads the support channels once; they are platform settings and do not change mid-visit. */
@Injectable({ providedIn: 'root' })
export class SupportService {
  private readonly http = inject(HttpClient);
  private loaded: Promise<SupportChannels> | null = null;

  load(): Promise<SupportChannels> {
    this.loaded ??= firstValueFrom(this.http.get<SupportChannels>('/v1/meta/support')).catch(
      (error: unknown) => {
        // A failure is not remembered: opening the window again asks again.
        this.loaded = null;
        throw error;
      },
    );
    return this.loaded;
  }
}
