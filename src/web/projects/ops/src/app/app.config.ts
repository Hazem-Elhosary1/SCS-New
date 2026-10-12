import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideScsCore } from 'core';
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
  ],
};
