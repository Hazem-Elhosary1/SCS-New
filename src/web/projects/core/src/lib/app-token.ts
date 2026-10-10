import { InjectionToken } from '@angular/core';

export type ScsApp = 'clinic' | 'patient' | 'ops';

/** Which of the three front ends is running. */
export const SCS_APP = new InjectionToken<ScsApp>('SCS_APP');
