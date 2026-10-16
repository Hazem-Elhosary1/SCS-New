import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';
import { SCS_ARABIC_DIGITS, provideScsCore } from 'core';
import { provideOverlayDirection } from 'ui';
import { routes } from './app.routes';
import { opsSessionInterceptor } from './auth/ops-auth.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideScsCore({ app: 'ops', interceptors: [opsSessionInterceptor] }),
    provideRouter(routes),
    // Menus and panels follow the language when it is switched without a reload.
    provideOverlayDirection(),
    // Western digits in the Arabic console (OP-B-386). The week starts on Saturday, as the clinic's
    // does (the default): the owner's choice over the frame's Sunday (75:4), 16 Oct 2026.
    { provide: SCS_ARABIC_DIGITS, useValue: 'latn' },
  ],
};
