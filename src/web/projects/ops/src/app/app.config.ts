import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideScsCore } from 'core';
import { provideOverlayDirection } from 'ui';
import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideScsCore({ app: 'ops' }),
    provideRouter(routes),
    // Menus and panels follow the language when it is switched without a reload.
    provideOverlayDirection(),
  ],
};
